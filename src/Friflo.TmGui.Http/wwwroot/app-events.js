// app-events.js

let boundCanvas = null;

// Send current canvas dimensions in physical GPU pixels as text stream
export function sendInitGui(ws, canvas) {
    if (!ws || ws.readyState !== WebSocket.OPEN || !canvas) return;

    const width = canvas.width;
    const height = canvas.height;

    // Key-value text payload matching C# ReadOnlySpan parser
    ws.send(`canvasWidth=${width};canvasHeight=${height};`);
}

// Resize Canvas to physical GPU pixels (HiDPI/Retina aware)
export function resizeCanvas(canvas, socket) {
    if (!canvas) return;

    const dpr = window.devicePixelRatio || 1;
    const pixelWidth = Math.max(1, Math.floor(window.innerWidth * dpr));
    const pixelHeight = Math.max(1, Math.floor(window.innerHeight * dpr));

    if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
        canvas.width = pixelWidth;
        canvas.height = pixelHeight;

        // Send updated dimensions to C# backend if WebSocket is active
        sendInitGui(socket, canvas);
    }
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
    window.addEventListener('resize', () => {
        resizeCanvas(boundCanvas, getSocket());
    });

    // 2. Unified Pointer Input Listeners (Mouse, Touch, Stylus)
    canvas.addEventListener('pointermove', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;

        const { x, y } = getCanvasCoords(e);
        socket.send(`evt=mousemove;mouseX=${x};mouseY=${y};`);
        
        // const time = (performance.timeOrigin + performance.now()) * 1e6; // high precision Unix time in nanoseconds
        // socket.send(`evt=mousemove;mouseX=${x};mouseY=${y};rttStart=${time}`);
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