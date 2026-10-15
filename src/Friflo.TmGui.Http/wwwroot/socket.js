// @ts-check
// socket.js
import { sendInitGui } from './app-events.js';
import { canvas, processMessage } from './app.js';

/** @type {WebSocket | null} */
let webSocket = null;

/** @type {RTCDataChannel | null} */
let dataChannel = null;


// Global WebSocket reference
export const socket = {
    isReady : function() {
        return webSocket !== null && webSocket.readyState === WebSocket.OPEN;
    },
    send : function(data) {
        webSocket.send(data);
    }
}

// ----------------------------------- WebSocket -----------------------------------

// Send initial canvas resolution to C# backend immediately after WebSocket handshake
export function initWebSocket()
{
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${wsProtocol}//${window.location.host}/ws`;
    const ws = new WebSocket(wsUrl);
    webSocket = ws;

    ws.binaryType = "arraybuffer";

    ws.onopen = () => {
        console.log("[+] WebSocket connected. Sending WsInitGui event...");
        
        // Send initial canvas resolution in zero-allocation key-value format
        sendInitGui(canvas);
    };

    ws.onclose = () => {
        console.log("[-] WebSocket connection closed.");
    };

    ws.onerror = (err) => {
        console.error("WebSocket error:", err);
    };
    
    ws.onmessage = (event) => {
        if (event.data instanceof ArrayBuffer) {
            // Create a lightweight zero-copy TypedArray view over the WebSocket ArrayBuffer
            processMessage(event.data);
        }
    };
}

// ------------------------------------ WebRTC ------------------------------------
export function initWebRTC(app)
{
    dataChannel = runSignaling(app)
}

/**
 * @param {string} app
 * @returns {RTCDataChannel}
 */
function runSignaling(app)
{
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const signalingUrl = `${wsProtocol}//${window.location.host}/signaling`;
    
    // 1. Create the signaling WebSocket connection inside the method
    const signalSocket = new WebSocket(signalingUrl);
    
    const pc = new RTCPeerConnection({
        iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
    });

    // Queue to buffer incoming ICE candidates until remote description is set
    /** @type {RTCIceCandidateInit[]} */
    const candidateQueue = [];

    // Optional: Create a data channel for peer-to-peer communication
    const dataChannel = pc.createDataChannel("tm-gui-channel");
    dataChannel.onopen = () => console.log("P2P Data Channel opened.");
    dataChannel.onmessage = (e) => console.log("Received P2P message:", e.data);

    // 2. Send the connect request as soon as the signaling socket is open
    signalSocket.addEventListener('open', () => {
        console.log("Connected to signaling server. Requesting app...");
        signalSocket.send(JSON.stringify({ type: "connect", app: app, sdpOrCandidate: null }));
    });

    // 3. Handle incoming messages from the signaling server
    signalSocket.addEventListener('message', async (event) => {
        /** @type {{ type: string, sdpOrCandidate?: string, app?: string }} */
        const message = JSON.parse(event.data);

        switch (message.type) {
            case "ConnectResponse":
                if (message.sdpOrCandidate === "Found") {
                    console.log(`Host '${app}' found. Creating WebRTC offer...`);
                    try {
                        const offer = await pc.createOffer();
                        await pc.setLocalDescription(offer);

                        signalSocket.send(JSON.stringify({ type: "offer", app:  app, sdpOrCandidate: offer.sdp }));
                    } catch (err) {
                        console.error("Failed to create WebRTC offer:", err);
                    }
                } else {
                    console.error(`Host '${app}' not found on server.`);
                }
                break;

            case "answer":
                try {
                    await pc.setRemoteDescription(new RTCSessionDescription({
                        type: "answer",
                        sdp: message.sdpOrCandidate
                    }));

                    // Flush queued candidates once remote description is set and connection is not yet established
                    while (candidateQueue.length > 0 && pc.connectionState !== 'connected' && pc.connectionState !== 'completed') {
                        const candidateInit = candidateQueue.shift();
                        if (candidateInit) {
                            try {
                                await pc.addIceCandidate(new RTCIceCandidate(candidateInit));
                            } catch (candErr) {
                                // Ignore stale or failing candidates
                            }
                        }
                    }
                } catch (err) {
                    console.error("Failed to set remote description (answer):", err);
                }
                break;

            case "candidate":
                try {
                    // Skip if connection is already established or completed
                    if (pc.connectionState === 'connected' || pc.connectionState === 'completed') {
                        break;
                    }

                    if (message.sdpOrCandidate) {
                        const candidateInit = JSON.parse(message.sdpOrCandidate);
                        
                        // Ignore empty or null end-of-candidates markers
                        if (!candidateInit || !candidateInit.candidate || candidateInit.candidate === "") {
                            break;
                        }

                        // Buffer candidates if remote description is not set yet
                        if (!pc.remoteDescription) {
                            candidateQueue.push(candidateInit);
                        } else {
                            await pc.addIceCandidate(new RTCIceCandidate(candidateInit));
                        }
                    }
                } catch (err) {
                    console.error("Failed to add ICE candidate:", err);
                }
                break;
        }
    });

    // 4. Send local ICE candidates to the peer via signaling server
    pc.onicecandidate = (event) => {
        if (event.candidate) {
            signalSocket.send(JSON.stringify({ type: "candidate", app: app, sdpOrCandidate: JSON.stringify(event.candidate) }));
        }
    };

    signalSocket.addEventListener('close', () => {
        console.log("Signaling WebSocket closed.");
    });

    signalSocket.addEventListener('error', (err) => {
        console.error("Signaling WebSocket error:", err);
    });

    return dataChannel;
}