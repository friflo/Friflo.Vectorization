// gui-events.js

let boundCanvas = null;
let boundSocket = null;

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

// Attach all Window & Canvas Event Listeners (Resize, Mouse, Keyboard)
export function initGuiEventListeners(canvas, getSocketFn) {
    boundCanvas = canvas;

    // Helper to always retrieve active socket reference
    const getSocket = () => (typeof getSocketFn === 'function' ? getSocketFn() : getSocketFn);

    // 1. Resize Listener
    window.addEventListener('resize', () => {
        resizeCanvas(boundCanvas, getSocket());
    });

    // 2. Mouse Input Listeners
    canvas.addEventListener('mousemove', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;

        const dpr = window.devicePixelRatio || 1;
        const x = e.clientX * dpr;
        const y = e.clientY * dpr;

        // TODO: Send mouse move payload to C# backend
        // socket.send(`evt=mousemove;x=${x};y=${y};`);
    });

    canvas.addEventListener('mousedown', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;

        // TODO: Send mouse down payload to C# backend
        // socket.send(`evt=mousedown;button=${e.button};`);
    });

    canvas.addEventListener('mouseup', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;

        // TODO: Send mouse up payload to C# backend
        // socket.send(`evt=mouseup;button=${e.button};`);
    });

    // 3. Keyboard Input Listeners
    window.addEventListener('keydown', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;

        // TODO: Send keydown payload to C# backend
        // socket.send(`evt=keydown;key=${e.key};code=${e.code};`);
    });

    window.addEventListener('keyup', (e) => {
        const socket = getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) return;

        // TODO: Send keyup payload to C# backend
        // socket.send(`evt=keyup;key=${e.key};code=${e.code};`);
    });
}