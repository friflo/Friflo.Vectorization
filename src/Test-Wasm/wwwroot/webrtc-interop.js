// ------------------------------------------- C# Interop -------------------------------------------
// socket.js
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

export function registerPeerConnectionCallbacks(pc, dotNetRef) {
    pc.ondatachannel = event => {
        dotNetRef.invokeMethodAsync('HandleDataChannel', event.channel);
    };
    pc.onicecandidate = event => {
        if (event.candidate) {
            dotNetRef.invokeMethodAsync('HandleIceCandidate', event.candidate);
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

export function setOrdered(channel, value) {
    channel.ordered = value;
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

export function sendBinary(channel, data, offset, length) {
    const view = data.subarray(offset, offset + length);
    channel.send(view);
}

export function registerDataChannelCallbacks(channel, dotNetRef) {
    channel.onopen = () => {
        dotNetRef.invokeMethodAsync('HandleStateChange', 'open');
    };
    channel.onclosing = () => {
        dotNetRef.invokeMethodAsync('HandleStateChange', 'closing');
    };
    channel.onclose = () => {
        dotNetRef.invokeMethodAsync('HandleClose');
    };
    channel.onmessage = event => {
        dotNetRef.invokeMethodAsync('HandleMessage', 0, event.data);
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