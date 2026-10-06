// app.js
import { sendInitGui, initGuiEventListeners, updateMouseCursor } from './app-events.js';

// Check WebGPU availability in current browser environment
if (!navigator.gpu) {
    console.error("WebGPU is not supported in this browser.");
    alert("WebGPU is not supported by your browser.");
}

const canvas = document.getElementById('gpu-canvas');

// WebGPU Core Objects
let device = null;
let context = null;
let presentationFormat = null;

// WebGPU Pipeline & Buffer Resources
let pipeline = null;
let bindGroupLayout = null;
let vertexBuffer = null;
let indexBuffer = null;
let uniformBuffer = null;
let bindGroup = null;
let dummyTextureView = null;
let dummySampler = null;


// Dynamic Storage Buffer Settings
let minStorageBufferOffsetAlignment = 256;
let dynamicUniformStride = 256;

// Global WebSocket reference
let socket = null;

// Texture Cache: Key = textureId, Value = { id, name, loaded, gpuTexture, bindGroup }
const textures = new Map();
const utf8Decoder = new TextDecoder('utf-8');

// Sizes matching C# Unsafe.SizeOf<T>() in bytes
const SIZEOF_INT = 4;
const SIZEOF_DOUBLE = 8;
const SIZEOF_WS_DRAW_COMMAND = 92; // 64 (projection) + 16 (scissor) + 8 (vertexView) + 4 (texture id)
const SIZEOF_MATRIX = 64;

// Initialize WebGPU context, fetch WGSL shader, create buffers & bind groups, and build render pipeline
async function initWebGPU() {
    const adapter = await navigator.gpu.requestAdapter();
    if (!adapter) {
        console.error("Failed to acquire WebGPU adapter.");
        return false;
    }

    device = await adapter.requestDevice();
    context = canvas.getContext('webgpu');

    minStorageBufferOffsetAlignment = device.limits.minStorageBufferOffsetAlignment || 256;
    dynamicUniformStride = Math.ceil(SIZEOF_MATRIX / minStorageBufferOffsetAlignment) * minStorageBufferOffsetAlignment;
    
    presentationFormat = navigator.gpu.getPreferredCanvasFormat();
    context.configure({
        device: device,
        format: presentationFormat,
        // 'opaque' Opaque mode disables transparency compositing in the OS compositor to reduce latency
        alphaMode: 'premultiplied',        
        // colorSpace: 'srgb' Prevents unnecessary color space conversions on the GPU
        // colorSpace: 'srgb',
        presentMode: 'immediate'
    });

    // 1. Create Initial Dynamic Vertex Buffer
    vertexBuffer = device.createBuffer({
        size: 65536,
        usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
    });

    // 2. Create Static Quad Index Buffer (0, 1, 2, 2, 3, 0 pattern)
    createStaticIndexBuffer(65536);

    // 3. Create Storage Buffer for ImUniforms struct (mat4x4<f32> = 64 bytes)
    uniformBuffer = device.createBuffer({
        size: Math.max(dynamicUniformStride * 256, 65536),
        usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST,
    });

    // 4. Create 1x1 White Fallback Texture & Sampler for u_texture & u_sampler
    const dummyTexture = device.createTexture({
        size: [1, 1],
        format: 'rgba8unorm',
        usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST,
    });
    device.queue.writeTexture(
        { texture: dummyTexture },
        new Uint8Array([255, 255, 255, 255]),
        { bytesPerRow: 4 },
        [1, 1]
    );
    dummyTextureView = dummyTexture.createView();
    dummySampler = device.createSampler({
        magFilter: 'linear',
        minFilter: 'linear'
    });

    // 5. Fetch WGSL shader file from wwwroot
    let shaderCode = "";
    try {
        const response = await fetch("draw2d.wgsl");
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        shaderCode = await response.text();
    } catch (err) {
        console.error("Failed to load WGSL shader file:", err);
        return false;
    }

    const shaderModule = device.createShaderModule({
        label: "Draw2D Shader Module",
        code: shaderCode
    });

    // 6. Configure vertex buffer layout (Vertex2D: pos=8B, uv=8B, color=4B)
    const vertexBufferLayout = {
        arrayStride: 20,
        attributes: [
            { shaderLocation: 0, offset: 0, format: 'float32x2' }, // position
            { shaderLocation: 1, offset: 8, format: 'float32x2' }, // uv
            { shaderLocation: 2, offset: 16, format: 'unorm8x4' }   // color
        ]
    };

    bindGroupLayout = device.createBindGroupLayout({
        label: "Draw2D BindGroupLayout",
        entries: [
            {
                binding: 0,
                visibility: GPUShaderStage.VERTEX,
                buffer: {
                    type: 'read-only-storage',
                    hasDynamicOffset: true,
                    minBindingSize: SIZEOF_MATRIX
                }
            },
            {
                binding: 1,
                visibility: GPUShaderStage.FRAGMENT,
                texture: { sampleType: 'float' }
            },
            {
                binding: 2,
                visibility: GPUShaderStage.FRAGMENT,
                sampler: { type: 'filtering' }
            }
        ]
    });

    const pipelineLayout = device.createPipelineLayout({
        bindGroupLayouts: [bindGroupLayout]
    });

    // 7. Create Render Pipeline
    pipeline = device.createRenderPipeline({
        label: "Draw2D Render Pipeline",
        layout: pipelineLayout,
        vertex: {
            module: shaderModule,
            entryPoint: 'vs_main',
            buffers: [vertexBufferLayout]
        },
        fragment: {
            module: shaderModule,
            entryPoint: 'fs_main',
            targets: [{
                format: presentationFormat,
                blend: {
                    color: {
                        srcFactor: 'src-alpha',
                        dstFactor: 'one-minus-src-alpha',
                        operation: 'add'
                    },
                    alpha: {
                        srcFactor: 'one',
                        dstFactor: 'one-minus-src-alpha',
                        operation: 'add'
                    }
                }
            }]
        },
        primitive: {
            topology: 'triangle-list',
            cullMode: 'none'
        }
    });

    // 8. Create Bind Group matching WGSL @group(0) bindings
    bindGroup = device.createBindGroup({
        layout: bindGroupLayout,
        entries: [
            { binding: 0, resource: { buffer: uniformBuffer, size: SIZEOF_MATRIX } },
            { binding: 1, resource: dummyTextureView },
            { binding: 2, resource: dummySampler }
        ]
    });

    console.log("[+] WebGPU device, Buffers, Uniforms, and BindGroup initialized successfully.");
    return true;
}

// Generate static Quad Index Buffer
function createStaticIndexBuffer(maxVertices = 65536) {
    const maxQuads = Math.floor(maxVertices / 4);
    const maxIndices = maxQuads * 6;
    const indices = new Uint32Array(maxIndices);

    for (let i = 0, v = 0; i < maxIndices; i += 6, v += 4) {
        indices[i + 0] = v + 0;
        indices[i + 1] = v + 1;
        indices[i + 2] = v + 2;
        indices[i + 3] = v + 2;
        indices[i + 4] = v + 3;
        indices[i + 5] = v + 0;
    }

    indexBuffer = device.createBuffer({
        size: indices.byteLength,
        usage: GPUBufferUsage.INDEX | GPUBufferUsage.COPY_DST,
        mappedAtCreation: true,
    });

    new Uint32Array(indexBuffer.getMappedRange()).set(indices);
    indexBuffer.unmap();
}

// Send initial canvas resolution to C# backend immediately after WebSocket handshake
function initWebSocket() {
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${wsProtocol}//${window.location.host}/ws`;
    socket = new WebSocket(wsUrl);

    socket.binaryType = "arraybuffer";

    socket.onopen = () => {
        console.log("[+] WebSocket connected. Sending WsInitGui event...");
        
        // Send initial canvas resolution in zero-allocation key-value format
        sendInitGui(socket, canvas);
    };

    socket.onclose = () => {
        console.log("[-] WebSocket connection closed.");
    };

    socket.onerror = (err) => {
        console.error("WebSocket error:", err);
    };
    
    socket.onmessage = (event) => {
        if (event.data instanceof ArrayBuffer) {
            // Create a lightweight zero-copy TypedArray view over the WebSocket ArrayBuffer
            processDrawList(new Uint8Array(event.data));
        }
    };
}

// ---------------------------------------------- processDrawList() ----------------------------------------------
// Keep comment - Key architecture:
// - Always process incoming messages
// - Render frames only at requestAnimationFrame() - The monitor refresh rate

function processDrawList(uint8Data)
{
    const view = new DataView(uint8Data.buffer);
    let offset = 0;
    
    // 0. Read rttStart time & host send time (double)
    // rttStart is send() via websocket at pointermove (mousemove) event in app-events.js
    const rttStartTime = view.getFloat64(offset, true);
    offset += SIZEOF_DOUBLE;
    
    const hostTime = view.getFloat64(offset, true);
    offset += SIZEOF_DOUBLE;

    // 1. Read drawCommands.Count (int)
    const drawCommandCount = view.getInt32(offset, true);
    offset += SIZEOF_INT;

    // 2. Read vertices.Length (int)
    const vertexCount = view.getInt32(offset, true);
    offset += SIZEOF_INT;
    
    // 3. Reach change count (int)
    const changeCount = view.getInt32(offset, true);
    offset += SIZEOF_INT;
    
    // 4. Read MouseCursor
    const mouseCursor = view.getInt32(offset, true);
    offset += SIZEOF_INT;
    
    // 5. Read used Textures (Count + ID/Name pairs)
    const newTexturesCount = view.getInt32(offset, true);
    offset += SIZEOF_INT;    
    for (let i = 0; i < newTexturesCount; i++) {
        // Keep comment: Each textureId is sent only once
        const textureId = view.getInt32(offset, true);
        offset += SIZEOF_INT;

        const nameByteLength = view.getInt32(offset, true);
        offset += SIZEOF_INT;

        // Zero-copy slice to decode string
        const nameBytes = uint8Data.subarray(offset, offset + nameByteLength);
        const name = utf8Decoder.decode(nameBytes);
        offset += nameByteLength;

        ensureTextureLoaded(textureId, name);
    }

    // 6. View on WsDrawCommand array
    const drawCommandsByteLength = drawCommandCount * SIZEOF_WS_DRAW_COMMAND;
    const drawCommandsOffset = offset;
    offset += drawCommandsByteLength;

    // 7. View on Vertex2D array
    const verticesByteLength = vertexCount * 20;
    const verticesOffset = offset;
    offset += verticesByteLength;
    
    // 8. View on change array
    const changeOffset = offset;
    offset += 8 * changeCount;    

    const terminator = view.getInt32(offset, true);
    offset += SIZEOF_INT;
    if (terminator !== 0x12345678) {
        console.error("found invalid terminator");
    }    
    
    updateMouseCursor(canvas, mouseCursor);
    
    const time = (performance.timeOrigin + performance.now()) * 1e6; // high precision Unix time in nanoseconds
    if (rttStartTime > 0) {
        const latency = (time - rttStartTime) / 1e6;
        console.log(`RTT latency: ${latency.toFixed(1)} ms`);
    }
    
    // -------- set state for next animationFrame() --------    
    const frame = backFrame;
    
    // Ensure global frame buffer is large enough and 4-byte aligned
    if (frame.frameBuffer.byteLength < uint8Data.byteLength) {
        // Align length up to the next multiple of 4 bytes
        const alignedLength = (uint8Data.byteLength + 3) & ~3;
        frame.frameBuffer           = new Uint8Array(alignedLength);
        frame.frameBufferDataView   = new DataView(frame.frameBuffer.buffer);
        frame.frameBufferFloatView  = new Float32Array(frame.frameBuffer.buffer);
    }
    
    // Copy incoming byte payload into persistent global buffer
    frame.frameBuffer.set(uint8Data, 0);
    frame.drawCommandsOffset    = drawCommandsOffset;
    frame.drawCommandCount      = drawCommandCount;
    frame.verticesOffset        = verticesOffset;
    frame.verticesByteLength    = verticesByteLength;
    
    triggerRender();
}

// Reusable global byte array buffer to eliminate per-frame heap allocations
let renderFrame = {
    frameBuffer          : new Uint8Array(0),
    frameBufferDataView  : new DataView(new Uint8Array(0).buffer),
    frameBufferFloatView : new Float32Array(0),
    
    drawCommandsOffset  : 0,
    drawCommandCount    : 0,
    verticesOffset      : 0,
    verticesByteLength  : 0,
}

// Ensure consistent frame state for each phase 
let backFrame  = { ... renderFrame };   // frame state (write) processDrawList() writes latest message to this state 
let frontFrame = { ... renderFrame };   // frame state (read)  submitDrawList() reads this state to create/submit() CommandEncoder

let isRenderPending = false;

// Keep comment:
// Request rendering with requestAnimationFrame() to prevent queueing render task in much higher rate WebGPU can handle.
// Only the latest renderFrame is relevant for rendering.
function triggerRender() {
    if (!isRenderPending) {
        isRenderPending = true;
        requestAnimationFrame(animationFrame);
    }
}

function animationFrame() {
    [frontFrame, backFrame] = [backFrame, frontFrame]; // swap frame buffers
    renderFrame = frontFrame;
    
    submitDrawList();
    isRenderPending = false;
}

// persistent render buffers
const submitQueue  = [null];
let uniformStaging = new Float32Array(0)

function submitDrawList()
{
    if (!device || !context || !pipeline || !bindGroup || !uniformBuffer || !indexBuffer) return;

    const frame = renderFrame;
    const view                  = frame.frameBufferDataView;
    const drawCommandsOffset    = frame.drawCommandsOffset
    const drawCommandCount      = frame.drawCommandCount;
    
    // Dynamic resize for Vertex Buffer if vertex payload exceeds current capacity
    if (vertexBuffer.size < frame.verticesByteLength) {
        vertexBuffer = device.createBuffer({
            size: frame.verticesByteLength,
            usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
        });
    }
    // Zero-allocation GPU upload directly from global frameBuffer with offset and length
    device.queue.writeBuffer(vertexBuffer, 0, frame.frameBuffer, frame.verticesOffset, frame.verticesByteLength);

    const requiredUniformBufferSize = drawCommandCount * dynamicUniformStride;
    if (uniformBuffer.size < requiredUniformBufferSize) {
        uniformBuffer = device.createBuffer({
            size: requiredUniformBufferSize,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST,
        });
        
        bindGroup = device.createBindGroup({
            layout: bindGroupLayout,
            entries: [
                { binding: 0, resource: { buffer: uniformBuffer, size: SIZEOF_MATRIX } },
                { binding: 1, resource: dummyTextureView },
                { binding: 2, resource: dummySampler }
            ]
        });

        textures.forEach(t => t.bindGroup = null);
    }
    
    const requiredStagingFloats = requiredUniformBufferSize / Float32Array.BYTES_PER_ELEMENT;
    if (uniformStaging.length < requiredStagingFloats) {
        uniformStaging = new Float32Array(requiredStagingFloats);
    }

    let cmdOffset = 0;
    const strideInFloats = dynamicUniformStride / Float32Array.BYTES_PER_ELEMENT;
    const frameBufferFloatView  = frame.frameBufferFloatView;


    for (let i = 0; i < drawCommandCount; i++) {
        const floatOffset = (drawCommandsOffset + cmdOffset) / Float32Array.BYTES_PER_ELEMENT;
        const targetOffset = i * strideInFloats;

        // Inlined zero-allocation matrix copy (16 floats / 64 bytes)
        for (let j = 0; j < 16; j++) {
            uniformStaging[targetOffset + j] = frameBufferFloatView[floatOffset + j];
        }

        cmdOffset += SIZEOF_WS_DRAW_COMMAND;
    }

    // Upload staging buffer to GPU in a single write operation
    device.queue.writeBuffer(uniformBuffer, 0, uniformStaging, 0, requiredStagingFloats);

    // Encode Render Pass
    const commandEncoder = device.createCommandEncoder();
    const pass = commandEncoder.beginRenderPass({
        colorAttachments: [{
            view: context.getCurrentTexture().createView(),
            clearValue: { r: 0.12, g: 0.12, b: 0.12, a: 1.0 },
            loadOp: 'clear',
            storeOp: 'store'
        }]
    });

    pass.setPipeline(pipeline);
    pass.setVertexBuffer(0, vertexBuffer);
    pass.setIndexBuffer(indexBuffer, 'uint32');

    cmdOffset = 0;
    const canvasWidth = canvas.width;
    const canvasHeight = canvas.height;

    for (let i = 0; i < drawCommandCount; i++) {
        const dynamicOffset = i * dynamicUniformStride;
        const currentCmdByteOffset = drawCommandsOffset + cmdOffset;

        // [Offset 64] scissor: RectVector2 (4 x float32 = 16 bytes: posX, posY, sizeX, sizeY)
        const posX  = view.getFloat32(currentCmdByteOffset + 64, true);
        const posY  = view.getFloat32(currentCmdByteOffset + 68, true);
        const sizeX = view.getFloat32(currentCmdByteOffset + 72, true);
        const sizeY = view.getFloat32(currentCmdByteOffset + 76, true);
    
        // [Offset 80] vertexView: MemoryView (2 x uint32 = 8 bytes: offset, count in vertices)
        const vertexOffset = view.getUint32(currentCmdByteOffset + 80, true);
        const vertexDrawCount = view.getUint32(currentCmdByteOffset + 84, true);
    
        // [Offset 88] textureId: uint32 (4 bytes)
        const textureId = view.getUint32(currentCmdByteOffset + 88, true);
    
        // Convert Vertex Count / Offset to Index Count / Offset
        const indexCount = Math.floor(vertexDrawCount / 4) * 6;
        const firstIndex = Math.floor(vertexOffset / 4) * 6;
    
        // Clamp Scissor Bounds to valid WebGPU viewport dimensions
        const clipX = Math.max(0, Math.min(Math.round(posX), canvasWidth));
        const clipY = Math.max(0, Math.min(Math.round(posY), canvasHeight));
        const clipWidth = Math.max(0, Math.min(Math.round(sizeX), canvasWidth - clipX));
        const clipHeight = Math.max(0, Math.min(Math.round(sizeY), canvasHeight - clipY));
    
        if (clipWidth > 0 && clipHeight > 0) {
            // Select texture BindGroup or fallback if texture is not ready yet
            const textureEntry = textures.get(textureId);
            const currentBindGroup = getTextureBindGroup(textureEntry);
    
            pass.setBindGroup(0, currentBindGroup, [dynamicOffset]);
            pass.setScissorRect(clipX, clipY, clipWidth, clipHeight);
            pass.drawIndexed(indexCount, 1, firstIndex, 0, 0);
        }
    
        cmdOffset += SIZEOF_WS_DRAW_COMMAND;
    }

    pass.end();
    
    submitQueue[0] = commandEncoder.finish();

    device.queue.submit(submitQueue);



    /* device.queue.onSubmittedWorkDone().then(() => {
        const gpuTime = performance.timeOrigin + performance.now();
        const realLatency = gpuTime - time;
        console.log(`REAL GPU latency: ${realLatency.toFixed(1)} ms`);
    }); */
}

function getTextureBindGroup(textureEntry) {
    if (!textureEntry || !textureEntry.loaded || !textureEntry.gpuTexture) {
        return bindGroup; // Fallback auf das globale BindGroup mit der 1x1 dummyTexture
    }

    if (!textureEntry.bindGroup) {
        // BindGroup für die spezifische Textur erzeugen und cachen
        textureEntry.bindGroup = device.createBindGroup({
            layout: bindGroupLayout,
            entries: [
                { binding: 0, resource: { buffer: uniformBuffer, size: SIZEOF_MATRIX } },
                { binding: 1, resource: textureEntry.gpuTexture.createView() },
                { binding: 2, resource: dummySampler }
            ]
        });
    }

    return textureEntry.bindGroup;
}

// Ensure texture is tracked and trigger async fetch from /textures/{name} if missing
function ensureTextureLoaded(textureId, name) {
    if (textures.has(textureId)) return;

    const textureEntry = {
        id: textureId,
        name: name,
        loaded: false,
        gpuTexture: null
    };

    textures.set(textureId, textureEntry);

    // Fetch image asynchronously from endpoint
    fetch(`/textures/${name}`)
        .then(response => {
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            return response.blob();
        })
        .then(blob => {
            return createImageBitmap(blob);
        })
        .then(imageBitmap => {
            // Create WebGPU Texture
            const gpuTexture = device.createTexture({
                size: [imageBitmap.width, imageBitmap.height, 1],
                format: 'rgba8unorm',
                usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST | GPUTextureUsage.RENDER_ATTACHMENT
            });

            device.queue.copyExternalImageToTexture(
                { source: imageBitmap },
                { texture: gpuTexture },
                [imageBitmap.width, imageBitmap.height]
            );

            // Free bitmap memory from host RAM
            imageBitmap.close();

            textureEntry.gpuTexture = gpuTexture;
            textureEntry.loaded = true;
            console.log(`[+] Texture loaded successfully: ${name} (ID: ${textureId})`);

            submitDrawList();
        })
        .catch(err => {
            console.error(`[-] Failed to load texture '${name}' (ID: ${textureId}):`, err);
        });
}

// Startup sequence
async function start() {
    // Register event listeners for resize, mouse, and keyboard inputs
    initGuiEventListeners(canvas, () => socket);

    const gpuReady = await initWebGPU();
    if (gpuReady) {
        initWebSocket();
    }
}

start();