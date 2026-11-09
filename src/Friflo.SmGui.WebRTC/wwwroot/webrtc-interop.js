// webrtc-interop.js
// noinspection JSUnusedGlobalSymbols

let WasmPeerConnection  = null; // Type: Friflo.SmGui.WebRTC.WasmPeerConnection
let WasmDataChannel     = null; // Type: Friflo.SmGui.WebRTC.WasmDataChannel
let Module              = null;
let WasmTimer           = null; // Type: Friflo.SmGui.WebRTC.WasmTimer

/**
 * Initializes SmGui WebRTC WASM for the passed .NET runtime.
 * 
 * Example usage in your index.html
 * ```html
<body>
    <script type="module">
    import { dotnet }                   from './_framework/dotnet.js';
    import { initializeSmGuiWebRTC }    from './webrtc-interop.js';

    // Create .NET WebAssembly Runtime
    const runtime = await dotnet.create();

    await initializeSmGuiWebRTC(runtime);
    </script>
</body>
 * ```
 */
export async function initializeSmGuiWebRTC(dotnetRuntime) {
    try {
        const exports = await dotnetRuntime.getAssemblyExports("Friflo.SmGui.WebRTC.dll");
        
        WasmPeerConnection      = exports.Friflo.SmGui.WebRTC.WasmPeerConnection;
        WasmDataChannel         = exports.Friflo.SmGui.WebRTC.WasmDataChannel;
        const WasmRtcFactory    = exports.Friflo.SmGui.WebRTC.WasmRtcFactory;
        WasmTimer               = exports.Friflo.SmGui.WebRTC.WasmTimer;
        
        Module = dotnetRuntime.Module;
        
        await WasmRtcFactory.InitializeAsync();
        
        console.log("SmGui WebRTC initialization successful.");
    } catch (err) {
        console.error("SmGui WebRTC initialization failed. Error:", err);
    }    
}

// --- WasmPeerConnection Interop ---

export function createPeerConnection() {
    return new RTCPeerConnection({
        iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
    });
}

export async function createAnswer(pc) {
    return await pc.createAnswer();
}

export async function createDataChannel(pc, label, channelId) {
    return await pc.createDataChannel(label, { ordered: true, negotiated: true, id: channelId });
}

export function getSdp(pc, description) {
    return description.sdp;
}

export async function setLocalDescription(pc, description) {
    await pc.setLocalDescription(description);
}

export async function setRemoteDescription(pc, description) {
    await pc.setRemoteDescription(description);
}

export async function addIceCandidate(pc, candidate) {
    await pc.addIceCandidate(candidate);
}

export function registerPeerConnectionCallbacks(instanceId, pc) {
    pc.onicecandidate = (event) => {
        if (event.candidate) {
            WasmPeerConnection.HandleIceCandidateCallback(instanceId, event.candidate);
        }
    };
}


// --- WasmDataChannel Interop ---
export function getState(channel) {
    return channel.readyState;
}

export function getOrdered(channel) {
    return channel.ordered;
}

export function getLabel(channel) {
    return channel.label;
}

export function setLabel(channel, value) {
    channel.label = value;
}

export function sendString(channel, message) {
    if (channel.readyState === "open") {
        channel.send(message);
        return true;
    }
    return false;
}

/* export function sendBinary(channel, ptr, offset, length) {
    if (channel.readyState === "open") {
        const memoryBuffer = Module.HEAPU8.buffer;
        const view = new Uint8Array(memoryBuffer, ptr + offset, length);
        channel.send(view);
        return true;
    }
    return false;
} */
export function sendBinary(channel, data, offset, length) {
    if (channel.readyState === "open") {
        const view = data.subarray(offset, offset + length);
        channel.send(view);
        return true;
    }
    return false;
}

export function registerDataChannelCallbacks(instanceId, channel) {
    channel.onopen = () => {
        WasmDataChannel.HandleStateChangeCallback(instanceId, channel.readyState);
    };
    channel.onclose = () => {
        WasmDataChannel.HandleCloseCallback(instanceId);
    };
    channel.onmessage = (event) => {
        const data = event.data;
        
        // Check if data is binary (ArrayBuffer or Blob) or a string
        if (typeof data === 'string') {
            // If your protocol supports string messages or text payload, 
            // map it appropriately (assuming protocol 0 or similar for text, adjust as needed)
            const encoder = new TextEncoder();
            const byteArray = encoder.encode(data);
            WasmDataChannel.HandleMessageCallback(instanceId, /* protocol for text */ 0, byteArray);
        } else {
            // Binary data (ArrayBuffer) -> convert to Uint8Array so .NET marshals it as byte[]
            const buffer = data instanceof ArrayBuffer ? data : data.arrayBuffer();
            
            // If arrayBuffer() is a Promise (in case of Blob), handle it asynchronously:
            if (buffer instanceof Promise) {
                buffer.then(ab => {
                    const byteArray = new Uint8Array(ab);
                    WasmDataChannel.HandleMessageCallback(instanceId, /* binary protocol enum value */ 1, byteArray);
                });
            } else {
                const byteArray = new Uint8Array(buffer);
                WasmDataChannel.HandleMessageCallback(instanceId, /* binary protocol enum value */ 1, byteArray);
            }
        }
    };
}

// --- WasmRtcFactory Interop ---
export function createSessionDescription(type, sdp) {
    return { type: type, sdp: sdp };
}

export function candidateToJson(candidate) {
    return JSON.stringify(candidate);
}

export function parseJsonToObj(json) {
    return JSON.parse(json);
}


// --- WasmTimer Interop ---
export function createTimer(instanceId)
{
    const worker = new Worker('timer.js');
    worker.onmessage = function (e) {
      if (e.data === 'tick') {
        WasmTimer.HandleTimerCallback(instanceId);
      }
    };
    return worker;
}

export function startTimer(timer)
{
    timer.postMessage("startTimer");
}

export function stopTimer(timer)
{
    timer.postMessage("stopTimer");
}

