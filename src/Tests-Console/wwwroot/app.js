// Check WebGPU availability in current browser environment
if (!navigator.gpu) {
    console.error("WebGPU is not supported in this browser.");
    alert("WebGPU is not supported by your browser.");
}

const canvas = document.getElementById('gpu-canvas');
let device = null;
let context = null;
let presentationFormat = null;
let vertexBuffer = null;
let pipeline = null;

// Sizes matching C# Unsafe.SizeOf<T>() in bytes
const SIZEOF_INT = 4;
const SIZEOF_WS_DRAW_COMMAND = 64; // Adjust to match C# WsDrawCommand struct layout exactly

// Initialize WebGPU context, buffers, and pipeline
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

    // Initial vertex buffer allocation (e.g. 64KB)
    vertexBuffer = device.createBuffer({
        size: 65536,
        usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
    });

    // TODO: Create your WebGPU render pipeline here and assign it to `pipeline`

    console.log("[+] WebGPU device initialized successfully.");
    return true;
}

// Initialize WebSocket connection to C# HttpServer
function initWebSocket() {
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${wsProtocol}//${window.location.host}/ws`;
    const socket = new WebSocket(wsUrl);

    // Expect binary frames for raw DrawList data
    socket.binaryType = "arraybuffer";

    socket.onopen = () => {
        console.log("[+] WebSocket connected to server.");
    };

    socket.onmessage = (event) => {
        if (event.data instanceof ArrayBuffer) {
            const drawListBuffer = new Uint8Array(event.data);
            
            // Execute WebGPU render pass using incoming DrawList
            processDrawList(drawListBuffer.buffer);
        }
    };

    socket.onclose = () => {
        console.log("[-] WebSocket connection closed.");
    };

    socket.onerror = (err) => {
        console.error("WebSocket error:", err);
    };

    // Forward keyboard input back to C# server
    window.addEventListener('keydown', (e) => {
        if (socket.readyState === WebSocket.OPEN) {
            socket.send(JSON.stringify({ type: 'keydown', key: e.key, code: e.code }));
        }
    });
}

// Process incoming binary DrawList frame and render via WebGPU
function processDrawList(arrayBuffer) {
    if (!device || !context || !pipeline) return;

    let offset = 0;
    const view = new DataView(arrayBuffer);

    // 1. Read drawCommands.Count (int)
    const drawCommandCount = view.getInt32(offset, true); // true = Little-Endian
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
    pass.setVertexBuffer(0, vertexBuffer);

    let cmdOffset = 0;
    const cmdView = new DataView(drawCommandsBuffer.buffer, drawCommandsBuffer.byteOffset, drawCommandsByteLength);

    for (let i = 0; i < drawCommandCount; i++) {
        const vertexOffset = cmdView.getUint32(cmdOffset + 0, true);
        const vertexDrawCount = cmdView.getUint32(cmdOffset + 4, true);

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