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
        if (webSocket !== null) {
            return webSocket.readyState === WebSocket.OPEN 
        }
        if (dataChannel !== null) {
            return dataChannel.readyState === "open";
        }
        return false;
    },
    send : function(data) {
        if (webSocket !== null) {
            webSocket.send(data);
            return;
        }
        if (dataChannel !== null) {
            dataChannel.send(data);
        }
    }
}

// ----------------------------------- overlays ------------------------------------
const logWindow = document.getElementById("log-window");

/** @param {string} info
 * @param {boolean} showLog */
export function addLog(info, showLog = true) {
    if (logWindow) {
        logWindow.textContent = logWindow.textContent + info;
        logWindow.scrollTop = logWindow.scrollHeight;
        if (showLog) logWindow.style.display = "block";
    }
}

function hideLogWindow() {
    if (logWindow)   logWindow.style.display = "none";
}


const infoOverlay = document.getElementById("info-overlay");

/** @param {string} info */
export function updateInfo(info) {
    if (infoOverlay) {
        infoOverlay.innerHTML = info;
        infoOverlay.style.display = "flex";
    }
}

function hideInfoOverlay() {
    if (infoOverlay) infoOverlay.style.display = "none";
}          



// ----------------------------------- WebSocket -----------------------------------

// Send initial canvas resolution to C# backend immediately after WebSocket handshake
export function initWebSocket()
{
    let receivedInitialFrame = false;
    addLog(`connecting ...`);
    
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${wsProtocol}//${window.location.host}/ws`;
    const ws = new WebSocket(wsUrl);
    webSocket = ws;

    ws.binaryType = "arraybuffer";

    ws.onopen = () => {
        console.log("[+] WebSocket connected. Sending WsInitGui event...");        
        // Send initial canvas resolution
        sendInitGui(canvas);
        addLog(` connected.\nstreaming started ...`);
    };

    ws.onclose = () => {
        console.log("[-] WebSocket connection closed.");
        addLog(`\ndisconnected.`, false);
        updateInfo(`disconnected`);
    };

    ws.onerror = (err) => {
        console.error("WebSocket error:", err);
    };
    
    ws.onmessage = (event) => {
        if (!receivedInitialFrame && event.data instanceof ArrayBuffer) {
            receivedInitialFrame = true;
            hideInfoOverlay();
            hideLogWindow()
        }
        processMessage(event.data);
    };
}

// ------------------------------------ WebRTC ------------------------------------
const availableApps = '<br><a href="/" style="text-decoration: underline;">available apps</a>';
let app = null;

export function setWindowTitle(title) {
    if (app) {
        document.title = `${app} - ${title}`;
    } else {
        document.title = title;
    }
}

export function initWebRTC(appName)
{
    app = appName;
    dataChannel = runSignaling(appName)
}

/**
 * @param {string} app
 * @returns {RTCDataChannel}
 */
function runSignaling(app)
{
    let receivedInitialFrame = false;
    addLog(`app '${app}'\napp lookup ...`);
                    
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
    const dataChannel = pc.createDataChannel("tm-gui-channel", { ordered: true });    
    pc.oniceconnectionstatechange = () => {
        const state = pc.iceConnectionState; 
        if (state === "connected" || state === "closed" || state === "completed" || state === "failed") {
            addLog(`\nsignaling finished: ${state}.`);
        }
    }
    pc.onconnectionstatechange = () => {
        console.log(`Connection state changed: ${pc.connectionState}`);
        
        if (pc.connectionState === "disconnected" || 
            pc.connectionState === "failed" || 
            pc.connectionState === "closed") {
            
            console.log(`Remote peer lost connection.`);
            addLog(`\napp ${pc.connectionState}`, false);
            updateInfo(`${app}&#10;${pc.connectionState}${availableApps}`);
        } else {
            const ellipsis = pc.connectionState === "connecting" ? " ..." : ".";
            addLog(`\napp ${pc.connectionState}${ellipsis}`, false);
        }
    };
    
    dataChannel.onopen = () => {
        console.log("P2P Data Channel opened.");
        // Send initial canvas resolution
        sendInitGui(canvas);
        addLog(`\napp channel opened.\napp streaming started ...`);
    }
    dataChannel.onclose = () => {
        console.log("P2P Data Channel closed.");
    }
    dataChannel.onerror = (ev) => {
        console.log("P2P Data Channel error.");
    }
    dataChannel.onmessage = async (e) => {
        if (e.data instanceof ArrayBuffer) {
            if (!receivedInitialFrame) {                
                receivedInitialFrame = true;
                hideInfoOverlay();
                hideLogWindow();
            }
            try {
                const rawData = await decompressMessage(e.data);
                
                console.log(`Received P2P message: ${e.data.byteLength}  uncompressed: ${rawData.byteLength}`);
                processMessage(rawData);
            } catch (err) {
                console.error("Failed to decompress message:", err);
            }
            return;
        }
        if (typeof e.data === 'string') {
            processMessage(e.data);
        }
    }

    // 2. Send the connect request as soon as the signaling socket is open
    signalSocket.addEventListener('open', () => {
        console.log("Connected to signaling server. Requesting app...");
        signalSocket.send(JSON.stringify({ type: "connect", app: app, sdpOrCandidate: null }));
    });
    let signalMsgCount = 0;

    // 3. Handle incoming messages from the signaling server
    signalSocket.addEventListener('message', async (event) => {
        /** @type {{ type: string, sdpOrCandidate?: string, app?: string }} */
        const message = JSON.parse(event.data);
        signalMsgCount++;

        switch (message.type) {
            case "ConnectResponse":
                if (message.sdpOrCandidate === "found") {
                    console.log(`app '${app}' found. Creating WebRTC offer...`);
                    addLog(` found.\nsignaling started (${signalMsgCount}) ...`);
                    try {
                        const offer = await pc.createOffer();
                        await pc.setLocalDescription(offer);

                        signalSocket.send(JSON.stringify({ type: "offer", app:  app, sdpOrCandidate: offer.sdp }));
                    } catch (err) {
                        console.error("Failed to create WebRTC offer:", err);
                    }
                } else {
                    console.error(`App '${app}' ${message.sdpOrCandidate}.`);
                    addLog(` ${message.sdpOrCandidate}.`);
                    updateInfo(`${app}&#10;${message.sdpOrCandidate}${availableApps}`);
                }
                break;

            case "answer":
                addLog(`\nsignaling [answer] (${signalMsgCount})`);
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
                addLog(`\nsignaling [candidate] (${signalMsgCount})`);
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


const blobArray= [null];

/**
 * @param {ArrayBuffer} arrayBuffer 
 * @returns {Promise<ArrayBuffer>}
 */
async function decompressMessage(arrayBuffer) {
    blobArray[0] = arrayBuffer;
    const stream = new Blob(blobArray).stream().pipeThrough(new DecompressionStream('gzip'));

    return await new Response(stream).arrayBuffer();

    /*
    // decompress without JS-Loop-Overhead.
    const stream = new ReadableStream({
        start(controller) {
            controller.enqueue(new Uint8Array(arrayBuffer));
            controller.close();
        }
    }).pipeThrough(new DecompressionStream('gzip'));
    
    return await new Response(stream).arrayBuffer();    
    */
}