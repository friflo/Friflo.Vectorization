// @ts-check
// app-events.js

let boundCanvas = null;

// Send current canvas dimensions in physical GPU pixels as text stream
export function sendInitGui(ws, canvas) {
    if (!ws || ws.readyState !== WebSocket.OPEN || !canvas) return;

    const width  = canvas.width;
    const height = canvas.height;

    // Key-value text payload matching C# ReadOnlySpan parser
    ws.send(`canvasWidth=${width};canvasHeight=${height};`);
}

// Resize Canvas to physical GPU pixels (HiDPI/Retina aware)
function initCanvasResize(canvas, getSocket) {
    if (!canvas) return;

    const resizeObserver = new ResizeObserver((entries) => {
        for (const entry of entries) {
            let width;
            let height;

            // Direct query of native physical display pixels (1:1 hardware pixel match)
            if (entry.devicePixelContentBoxSize && entry.devicePixelContentBoxSize.length > 0) {
                width  = entry.devicePixelContentBoxSize[0].inlineSize;
                height = entry.devicePixelContentBoxSize[0].blockSize;
            } else {
                // Fallback for browsers without devicePixelContentBoxSize support
                const dpr = window.devicePixelRatio || 1;
                const rect = canvas.getBoundingClientRect();
                width  = Math.round(rect.width * dpr);
                height = Math.round(rect.height * dpr);
            }
            
            // Update canvas dimensions and notify C# backend if resolution changed
            if (canvas.width !== width || canvas.height !== height) {
                canvas.width  = width;
                canvas.height = height;
                const socket = getSocket();
                sendInitGui(socket, canvas);
            }
        }
    });

    resizeObserver.observe(canvas);
}


// Map C# MouseCursor enum values to standard CSS cursor strings
const MOUSE_CURSORS = [
    'default',      // 0: Arrow
    'ns-resize',    // 1: ResizeN
    'ns-resize',    // 2: ResizeS
    'ew-resize',    // 3: ResizeE
    'ew-resize',    // 4: ResizeW
    'nwse-resize',  // 5: ResizeNW
    'nwse-resize',  // 6: ResizeSE
    'nesw-resize',  // 7: ResizeNE
    'nesw-resize'   // 8: ResizeSW
];

export function updateMouseCursor(canvas, cursorIndex) {
    if (!canvas) return;

    const cssCursor = MOUSE_CURSORS[cursorIndex] || 'default';
    
    if (canvas.style.cursor !== cssCursor) {
        canvas.style.cursor = cssCursor;
    }
}

// Attach all Window & Canvas Event Listeners (Resize, Pointer, Keyboard)
export function initGuiEventListeners(canvas, getSocketFn) {
    boundCanvas = canvas;

    // Prevent default touch gestures (pinch-zoom, scrolling) on canvas
    canvas.style.touchAction = 'none';

    // Helper to always retrieve active socket reference
    const getSocket = () => (typeof getSocketFn === 'function' ? getSocketFn() : getSocketFn);

    // Helper to calculate exact physical GPU pixel coordinates relative to canvas
    const getCanvasCoords = (e) => {
        const rect = canvas.getBoundingClientRect();
        const dpr = window.devicePixelRatio || 1;
        const x = (e.clientX - rect.left) * dpr;
        const y = (e.clientY - rect.top) * dpr;
        return { x: Math.round(x), y: Math.round(y) };
    };

    // 1. Resize Listener
    initCanvasResize(canvas, getSocket);

    
    let lastPointerEvent = null;
    let isPointerFrameScheduled = false;

    canvas.addEventListener('pointermove', (e) => {
        lastPointerEvent = e;
    
        if (!isPointerFrameScheduled) {
            isPointerFrameScheduled = true;
    
            requestAnimationFrame(() => {
                isPointerFrameScheduled = false;
    
                const socket = getSocket();
                if (!socket || socket.readyState !== WebSocket.OPEN) return;
    
                const { x, y } = getCanvasCoords(lastPointerEvent);
                socket.send(`evt=mousemove;mouseX=${x};mouseY=${y};`);
                
                // const time = (performance.timeOrigin + performance.now()) * 1e6; // high precision Unix time in nanoseconds
                // socket.send(`evt=mousemove;mouseX=${x};mouseY=${y};rttStart=${time}`);
            });
        }
    });

    canvas.addEventListener('pointerdown', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;

        // Capture pointer events even if touch moves outside canvas boundaries
        canvas.setPointerCapture(e.pointerId);

        const { x, y } = getCanvasCoords(e);
        socket.send(`evt=mousedown;button=${e.button};mouseX=${x};mouseY=${y};`);
    });

    canvas.addEventListener('pointerup', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;

        if (canvas.hasPointerCapture(e.pointerId)) {
            canvas.releasePointerCapture(e.pointerId);
        }

        const { x, y } = getCanvasCoords(e);
        socket.send(`evt=mouseup;button=${e.button};mouseX=${x};mouseY=${y};`);
    });
    
    window.addEventListener('wheel', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;
        
        // Prevent default page scrolling if canvas should capture all scroll events
        e.preventDefault();
    
        socket.send(`evt=wheel;deltaX=${e.deltaX};deltaY=${e.deltaY};deltaMode=${e.deltaMode};`);
    }, { passive: true });

    // 3. Keyboard Input Listeners
    window.addEventListener('keydown', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;
        
        if (e.key === 'Tab') e.preventDefault()

        socket.send(`evt=keydown;key=${e.key};code=${e.code};`);
    });

    window.addEventListener('keyup', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;

        socket.send(`evt=keyup;key=${e.key};code=${e.code};`);
    });
}


// Persistent Single Source of Truth on CPU and separate Staging Buffer
export let masterVertices   = new Uint8Array(0);
let masterByteLength        = 0;
let stagingVerticesBuffer   = new Uint8Array(0);

/**
 * Applies binary diffs (modify, insert, remove) onto the CPU master vertex state.
 *
 * @param {Uint8Array} uint8Data        - The incoming binary WebSocket packet buffer.
 * @param {DataView} view               - DataView mapped to the incoming packet buffer for reading change structures.
 * @param {number} changeOffset         - Byte offset in the buffer where the array of SeqChange structures begins.
 * @param {number} changeCount          - Total number of diff changes to apply.
 * @param {number} diffVertexCount      - Total number of all vertices for entire frame. Or -1 for full update (no diffs)
 * @param {number} verticesOffset       - Byte offset in the buffer where incoming vertex diff payloads start.
 * @param {number} verticesByteLength   - Byte length of the incoming vertex data slice.
 * @returns {number} masterByteLength
 */
export function applyVertexChanges(uint8Data, view, changeOffset, changeCount, diffVertexCount, verticesOffset, verticesByteLength) {
    const BYTES_PER_QUAD = 80; // 4 Vertices * 20 Bytes

    // Fast-path: Complete full update (no diffs)
    if (diffVertexCount === -1) {
        if (masterVertices.byteLength < verticesByteLength) {
            const alignedLength = (verticesByteLength + 1024 + 3) & ~3;
            masterVertices = new Uint8Array(alignedLength);
        }
        masterVertices.set(uint8Data.subarray(verticesOffset, verticesOffset + verticesByteLength), 0);
        masterByteLength = verticesByteLength;
        return masterByteLength;
    }
    // Fast-path: Change diffs are empty - nothing to apply
    if (changeCount === 0) {
        return masterByteLength;
    }

    const startMasterQuads = masterByteLength / BYTES_PER_QUAD;
    const targetByteLength = diffVertexCount * 20;

    // Ensure staging buffer has capacity to write the upcoming frame
    if (stagingVerticesBuffer.byteLength < targetByteLength) {
        const alignedLength = (targetByteLength + 1024 + 3) & ~3;
        stagingVerticesBuffer = new Uint8Array(alignedLength);
    }

    const startState  = masterVertices;
    const targetState = stagingVerticesBuffer;

    // Exact logic mirror of C# ApplyChanges<T>
    let readOffsetQuad   = 0;
    let writeOffsetQuad  = 0;
    let diffOffsetByte   = verticesOffset; // Payload in uint8Data starts at verticesOffset
    let readChangeOffset = changeOffset;

    for (let c = 0; c < changeCount; c++) {
        // Read SeqChange (24-bit quadStart, 8-bit type, 32-bit quadLength)
        const quadStart = view.getUint8(readChangeOffset) |
                         (view.getUint8(readChangeOffset + 1) << 8) |
                         (view.getUint8(readChangeOffset + 2) << 16);
        const type = view.getUint8(readChangeOffset + 3);
        const quadLength = view.getInt32(readChangeOffset + 4, true);
        readChangeOffset += 8;

        // 1. Copy unmodified items leading up to this change
        const unmodifiedCount = quadStart - readOffsetQuad;
        if (unmodifiedCount > 0) {
            const unmodifiedBytes = unmodifiedCount * BYTES_PER_QUAD;
            const srcByteOffset   = readOffsetQuad * BYTES_PER_QUAD;
            const dstByteOffset   = writeOffsetQuad * BYTES_PER_QUAD;

            targetState.set(startState.subarray(srcByteOffset, srcByteOffset + unmodifiedBytes), dstByteOffset);

            readOffsetQuad  += unmodifiedCount;
            writeOffsetQuad += unmodifiedCount;
        }

        // 2. Process SeqChangeType
        const changeBytes   = quadLength * BYTES_PER_QUAD;
        const dstByteOffset = writeOffsetQuad * BYTES_PER_QUAD;

        if (type === 1 || type === 2) { // Modify or Insert
            // Copy new/updated payload from diffValues (uint8Data)
            targetState.set(uint8Data.subarray(diffOffsetByte, diffOffsetByte + changeBytes), dstByteOffset);

            diffOffsetByte  += changeBytes;
            writeOffsetQuad += quadLength;

            if (type === 1) { // Modify
                readOffsetQuad += quadLength;
            }
        } else if (type === 3) { // Remove
            readOffsetQuad += quadLength;
        }
    }

    // 3. Copy remaining tail elements if any
    const remainingCount = startMasterQuads - readOffsetQuad;
    if (remainingCount > 0) {
        const remainingBytes = remainingCount  * BYTES_PER_QUAD;
        const srcByteOffset  = readOffsetQuad  * BYTES_PER_QUAD;
        const dstByteOffset  = writeOffsetQuad * BYTES_PER_QUAD;

        targetState.set(startState.subarray(srcByteOffset, srcByteOffset + remainingBytes), dstByteOffset);

        writeOffsetQuad += remainingCount;
    }

    const newByteLength = writeOffsetQuad * BYTES_PER_QUAD;

    if (targetByteLength !== newByteLength) {
        console.error(`invalid vertex length. expect: ${targetByteLength}  was: ${newByteLength}`);
    }

    // Zero-Copy Strategy: (this comment must not be deleted)
    // Perform an atomic pointer swap instead of copying memory via .set().
    // We swap ONLY AFTER fully constructing the new frame in targetState (staging)
    // to ensure masterVertices remains intact if an error occurs mid-processing.
    //
    // Note: If a length mismatch or error occurs during diff processing, we still commit the swap.
    // Subsequent frames quads could repair any partially corrupted vertex state automatically.
    stagingVerticesBuffer = startState;
    masterVertices        = targetState;
    masterByteLength      = targetByteLength;

    return masterByteLength;
}