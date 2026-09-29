// Check WebGPU availability in current browser environment
if (!navigator.gpu) {
    console.error("WebGPU is not supported in this browser.");
    alert("WebGPU is not supported by your browser.");
}

const canvas = document.getElementById('gpu-canvas');
let device = null;
let context = null;
let presentationFormat = null;

// Initialize WebGPU context
async function initWebGPU() {
    const adapter = await navigator.gpu.requestAdapter();
    if (!adapter) {
        console.error("Failed to acquire WebGPU adapter.");
        return false;
    }

    device = await adapter.requestDevice();
    context = canvas.getContext('webgpu');
    
    // Configure canvas swapchain format (typically 'bgra8unorm' or 'rgba8unorm')
    presentationFormat = navigator.gpu.getPreferredCanvasFormat();
    context.configure({
        device: device,
        format: presentationFormat,
        alphaMode: 'premultiplied'
    });

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
            renderDrawList(drawListBuffer);
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

// Render loop for incoming binary DrawList frame
function renderDrawList(buffer) {
    if (!device || !context) return;

    // Create command encoder for the current frame
    const commandEncoder = device.createCommandEncoder();
    const textureView = context.getCurrentTexture().createView();

    // Define standard render pass descriptor
    const renderPassDescriptor = {
        colorAttachments: [
            {
                view: textureView,
                clearValue: { r: 0.12, g: 0.12, b: 0.12, a: 1.0 },
                loadOp: 'clear',
                storeOp: 'store'
            }
        ]
    };

    const passEncoder = commandEncoder.beginRenderPass(renderPassDescriptor);

    // TODO: Parse draw commands from buffer, update GPU buffers (vertex/index), and submit draw calls
    // e.g., passEncoder.setPipeline(pipeline);
    // e.g., passEncoder.draw(vertexCount);

    passEncoder.end();
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