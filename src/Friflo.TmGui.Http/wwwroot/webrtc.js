// @ts-check
// webrtc.js

/**
 * Initializes the signaling WebSocket and the WebRTC peer connection as a client.
 * @param {string} hostName 
 */
export function initWebRTC(hostName)
{
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const signalingUrl = `${wsProtocol}//${window.location.host}/signaling`;
    
    // 1. Create the signaling WebSocket connection inside the method
    const socket = new WebSocket(signalingUrl);
    
    const pc = new RTCPeerConnection({
        iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
    });

    // Optional: Create a data channel for peer-to-peer communication
    const dataChannel = pc.createDataChannel("tm-gui-channel");
    dataChannel.onopen = () => console.log("P2P Data Channel opened.");
    dataChannel.onmessage = (e) => console.log("Received P2P message:", e.data);

    // 2. Send the connect request as soon as the signaling socket is open
    socket.addEventListener('open', () => {
        console.log("Connected to signaling server. Requesting host...");
        socket.send(JSON.stringify({
            type: "connect",
            sdpOrCandidate: null,
            host: hostName
        }));
    });

    // 3. Handle incoming messages from the signaling server
    socket.addEventListener('message', async (event) => {
        /** @type {{ type: string, sdpOrCandidate?: string, host?: string }} */
        const message = JSON.parse(event.data);

        switch (message.type) {
            case "ConnectResponse":
                if (message.sdpOrCandidate === "Found") {
                    console.log(`Host '${hostName}' found. Creating WebRTC offer...`);
                    try {
                        const offer = await pc.createOffer();
                        await pc.setLocalDescription(offer);

                        socket.send(JSON.stringify({
                            type: "offer",
                            sdpOrCandidate: offer.sdp,
                            host: hostName
                        }));
                    } catch (err) {
                        console.error("Failed to create WebRTC offer:", err);
                    }
                } else {
                    console.error(`Host '${hostName}' not found on server.`);
                }
                break;

            case "answer":
                try {
                    await pc.setRemoteDescription(new RTCSessionDescription({
                        type: "answer",
                        sdp: message.sdpOrCandidate
                    }));
                } catch (err) {
                    console.error("Failed to set remote description (answer):", err);
                }
                break;

            case "candidate":
                try {
                    if (message.sdpOrCandidate) {
                        const candidateInit = JSON.parse(message.sdpOrCandidate);
                        await pc.addIceCandidate(new RTCIceCandidate(candidateInit));
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
            socket.send(JSON.stringify({
                type: "candidate",
                sdpOrCandidate: JSON.stringify(event.candidate),
                host: hostName
            }));
        }
    };

    socket.addEventListener('close', () => {
        console.log("Signaling WebSocket closed.");
    });

    socket.addEventListener('error', (err) => {
        console.error("Signaling WebSocket error:", err);
    });

    return { socket, pc, dataChannel };
}