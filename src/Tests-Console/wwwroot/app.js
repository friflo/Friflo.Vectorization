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

// WebGPU Pipeline & Buffer Resources (Global Module Scope)
let pipeline = null;
let vertexBuffer = null;
let uniformBuffer = null;
let bindGroup = null;
let dummyTextureView = null;
let dummySampler = null;

// Sizes matching C# Unsafe.SizeOf<T>() in bytes
const SIZEOF_INT = 4;
const SIZEOF_WS_DRAW_COMMAND = 64; // Adjust to match C# WsDrawCommand struct layout exactly

// Initialize WebGPU context, fetch WGSL shader, create buffers & bind groups, and build render pipeline
async function initWebGPU() {
    const adapter = await navigator.gpu.requestAdapter();
    if (!adapter) {
        console.error("Failed to acquire WebGPU adapter.");
        return false;
    }

    device = await adapter.requestDevice();
    context = canvas.getContext('webgpu');
    
    presentationFormat = navigator.gpu.getPreferredCanvasFormat();
    context.configure({
        device: device,
        format: presentationFormat,
        alphaMode: 'premultiplied'
    });

    // Handle canvas resizing
    function resizeCanvas() {
        const width = Math.max(1, window.innerWidth);
        const height = Math.max(1, window.innerHeight);
        canvas.width = width;
        canvas.height = height;
    }
    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);

    // 1. Create Initial Vertex Buffer
    vertexBuffer = device.createBuffer({
        size: 65536,
        usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
    });

    // 2. Create Uniform Buffer for ImUniforms struct (mat4x4<f32> = 64 bytes)
    uniformBuffer = device.createBuffer({
        size: 64,
        usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
    });

    // 3. Create 1x1 White Fallback Texture & Sampler for u_texture & u_sampler
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

    // 4. Fetch WGSL shader file from wwwroot
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

    // 5. Configure vertex buffer layout (Vertex2D: pos=8B, uv=8B, color=4B)
    const vertexBufferLayout = {
        arrayStride: 20,
        attributes: [
            { shaderLocation: 0, offset: 0, format: 'float32x2' }, // position
            { shaderLocation: 1, offset: 8, format: 'float32x2' }, // uv
            { shaderLocation: 2, offset: 16, format: 'unorm8x4' }   // color
        ]
    };

    // 6. Create Render Pipeline
    pipeline = device.createRenderPipeline({
        label: "Draw2D Render Pipeline",
        layout: 'auto',
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

    // 7. Create Bind Group matching WGSL @group(0) bindings
    bindGroup = device.createBindGroup({
        layout: pipeline.getBindGroupLayout(0),
        entries: [
            { binding: 0, resource: { buffer: uniformBuffer } },
            { binding: 1, resource: dummyTextureView },
            { binding: 2, resource: dummySampler }
        ]
    });

    console.log("[+] WebGPU device, Uniforms, and BindGroup initialized successfully.");
    return true;
}

// Initialize WebSocket connection to C# HttpServer
function initWebSocket() {
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${wsProtocol}//${window.location.host}/ws`;
    const socket = new WebSocket(wsUrl);

    socket.binaryType = "arraybuffer";

    socket.onopen = () => {
        console.log("[+] WebSocket connected to server.");
    };

    socket.onmessage = (event) => {
        if (event.data instanceof ArrayBuffer) {
            processDrawList(event.data);
        }
    };

    socket.onclose = () => {
        console.log("[-] WebSocket connection closed.");
    };

    socket.onerror = (err) => {
        console.error("WebSocket error:", err);
    };

    window.addEventListener('keydown', (e) => {
        if (socket.readyState === WebSocket.OPEN) {
            socket.send(JSON.stringify({ type: 'keydown', key: e.key, code: e.code }));
        }
    });
}

// Process incoming binary DrawList frame and render via WebGPU
function processDrawList(arrayBuffer) {
    if (!device || !context || !pipeline || !bindGroup || !uniformBuffer) return;

    let offset = 0;
    const view = new DataView(arrayBuffer);

    // 1. Read drawCommands.Count (int)
    const drawCommandCount = view.getInt32(offset, true);
    offset += SIZEOF_INT;

    // 2. Read vertices.Length (int)
    const vertexCount = view.getInt32(offset, true);
    offset += SIZEOF_INT;

    // 3. View on WsDrawCommand array (Zero-Copy)
    const drawCommandsByteLength = drawCommandCount * SIZEOF_WS_DRAW_COMMAND;
    const drawCommandsBuffer = new Uint8Array(arrayBuffer, offset, drawCommandsByteLength);
    offset += drawCommandsByteLength;

    // 4. View on Vertex2D array (Zero-Copy slice directly uploaded to GPU)
    const verticesSlice = new Uint8Array(arrayBuffer, offset);

    // Upload vertices directly into GPU Vertex Buffer
    if (vertexBuffer.size < verticesSlice.byteLength) {
        vertexBuffer = device.createBuffer({
            size: verticesSlice.byteLength,
            usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
        });
    }
    device.queue.writeBuffer(vertexBuffer, 0, verticesSlice);

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
    pass.setBindGroup(0, bindGroup);
    pass.setVertexBuffer(0, vertexBuffer);

    let cmdOffset = 0;
    const cmdView = new DataView(drawCommandsBuffer.buffer, drawCommandsBuffer.byteOffset, drawCommandsByteLength);

    for (let i = 0; i < drawCommandCount; i++) {
        // Extract vertex view fields matching C# WsDrawCommand layout
        const vertexOffset = cmdView.getUint32(cmdOffset + 0, true);
        const vertexDrawCount = cmdView.getUint32(cmdOffset + 4, true);

        // Extract projection matrix (16 floats = 64 bytes) directly from WsDrawCommand (offset 8)
        const matrixByteOffset = drawCommandsBuffer.byteOffset + cmdOffset + 8;
        const projectionMatrix = new Float32Array(arrayBuffer, matrixByteOffset, 16);

        // Upload command's projection matrix directly to GPU uniform buffer
        device.queue.writeBuffer(uniformBuffer, 0, projectionMatrix);

        // Draw vertices
        pass.draw(vertexDrawCount, 1, vertexOffset, 0);

        cmdOffset += SIZEOF_WS_DRAW_COMMAND;
    }

    pass.end();
    device.queue.submit([commandEncoder.finish()]);
}

// Startup sequence
async function start() {
    const gpuReady = await initWebGPU();
    if (gpuReady) {
        initWebSocket();
    }
}

start();