// webrtc-interop.js
// noinspection JSUnusedGlobalSymbols

let WasmPeerConnection  = null; // Type: Friflo.SmGui.WebRTC.WasmPeerConnection
let WasmDataChannel     = null; // Type: Friflo.SmGui.WebRTC.WasmDataChannel
let Module              = null;

// Called from index.html right after .NET runtime creation
export async function initializeInterop(getAssemblyExports) {
    try {
        const dotnetExports = await getAssemblyExports("Friflo.SmGui.WebRTC.dll");
        const WebRTC = dotnetExports.Friflo.SmGui.WebRTC;
        WasmPeerConnection = WebRTC.WasmPeerConnection;
        WasmDataChannel    = WebRTC.WasmDataChannel;
        
        const runtime = globalThis.getDotnetRuntime(0);
        Module = runtime.Module;
        
        await WebRTC.WasmRtcFactory.InitializeAsync();
        console.log(".NET initialization completed successfully in JS!");
    } catch (err) {
        console.error("Failed to initialize .NET WebAssembly:", err);
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
    pc.ondatachannel = (event) => {
        // Access the exported C# class and call the [JSExport] method
        WasmPeerConnection.HandleDataChannelCallback(instanceId, event.channel);
    };
    
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
    channel.send(message);
}

/* export function sendBinary(channel, ptr, offset, length) {
    const memoryBuffer = Module.HEAPU8.buffer;
    const view = new Uint8Array(memoryBuffer, ptr + offset, length);
    channel.send(view);
} */
export function sendBinary(channel, data, offset, length) {
    const view = data.subarray(offset, offset + length);
    channel.send(view);
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