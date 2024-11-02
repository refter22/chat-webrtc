class WebRTCManager {
    constructor() {
        this.connections = new Map();
    }

    async initialize(dotNetRef, isInitiator, targetUserId) {
        const connection = {
            peerConnection: new RTCPeerConnection({
                iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
            }),
            dataChannel: null,
            dotNetRef: dotNetRef
        };

        connection.peerConnection.onicecandidate = async (event) => {
            if (event.candidate) {
                await dotNetRef.invokeMethodAsync(
                    'HandleIceCandidate',
                    targetUserId,
                    event.candidate
                );
            }
        };

        if (isInitiator) {
            connection.dataChannel =
                connection.peerConnection.createDataChannel('dataChannel');
            this.setupDataChannel(
                connection.dataChannel,
                dotNetRef,
                targetUserId
            );
        } else {
            connection.peerConnection.ondatachannel = (event) => {
                connection.dataChannel = event.channel;
                this.setupDataChannel(
                    connection.dataChannel,
                    dotNetRef,
                    targetUserId
                );
            };
        }

        this.connections.set(targetUserId, connection);
        return true;
    }

    setupDataChannel(channel, dotNetRef, targetUserId) {
        channel.onopen = () => {
            console.log(`DataChannel opened for ${targetUserId}`);
            dotNetRef.invokeMethodAsync('HandleConnectionOpened', targetUserId);
        };

        channel.onclose = () => {
            console.log(`DataChannel closed for ${targetUserId}`);
            dotNetRef.invokeMethodAsync('HandleConnectionClosed', targetUserId);
        };

        channel.onmessage = (event) => {
            console.log(`Received message from ${targetUserId}:`, event.data);
            dotNetRef.invokeMethodAsync(
                'HandleDataReceived',
                targetUserId,
                event.data
            );
        };
    }

    async createOffer(targetUserId) {
        const connection = this.connections.get(targetUserId);
        const offer = await connection.peerConnection.createOffer();
        await connection.peerConnection.setLocalDescription(offer);
        return offer;
    }

    async handleOffer(targetUserId, offer) {
        const connection = this.connections.get(targetUserId);
        await connection.peerConnection.setRemoteDescription(
            new RTCSessionDescription(offer)
        );
        const answer = await connection.peerConnection.createAnswer();
        await connection.peerConnection.setLocalDescription(answer);
        return answer;
    }

    async handleAnswer(targetUserId, answer) {
        const connection = this.connections.get(targetUserId);
        await connection.peerConnection.setRemoteDescription(
            new RTCSessionDescription(answer)
        );
    }

    async addIceCandidate(targetUserId, candidate) {
        const connection = this.connections.get(targetUserId);
        await connection.peerConnection.addIceCandidate(
            new RTCIceCandidate(candidate)
        );
    }

    async sendData(targetUserId, data) {
        const connection = this.connections.get(targetUserId);
        if (connection?.dataChannel?.readyState === 'open') {
            console.log(`Sending data to ${targetUserId}:`, data);
            connection.dataChannel.send(data);
            return true;
        }
        console.warn(
            `Cannot send data to ${targetUserId}, connection not ready`
        );
        return false;
    }

    dispose(targetUserId) {
        const connection = this.connections.get(targetUserId);
        if (connection) {
            if (connection.dataChannel) {
                connection.dataChannel.close();
            }
            if (connection.peerConnection) {
                connection.peerConnection.close();
            }
            this.connections.delete(targetUserId);
        }
    }
}

window.webrtc = new WebRTCManager();
