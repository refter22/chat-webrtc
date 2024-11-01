class WebRTCManager {
    constructor() {
        this.peerConnection = null;
        this.dataChannel = null;
        this.dotNetRef = null;
        this.isInitialized = false;
        this.isConnected = false;
    }

    setupDataChannelHandlers(channel) {
        channel.onopen = async () => {
            this.isConnected = true;
            this.dataChannel = channel;
            if (this.dotNetRef) {
                await this.dotNetRef.invokeMethodAsync('HandleDataChannelOpen');
                await this.dotNetRef.invokeMethodAsync('HandleWebRTCConnected');
            }
        };

        channel.onclose = async () => {
            this.isConnected = false;
            this.dataChannel = null;
            if (this.dotNetRef) {
                await this.dotNetRef.invokeMethodAsync('HandleWebRTCClosed');
            }
        };

        channel.onmessage = async (event) => {
            if (this.dotNetRef) {
                await this.dotNetRef.invokeMethodAsync('HandleWebRTCMessage', event.data);
            }
        };
    }

    async initialize(dotNetRef, isInitiator = false) {
        try {
            this.dotNetRef = dotNetRef;

            const configuration = {
                iceServers: [
                    { urls: 'stun:stun.l.google.com:19302' }
                ]
            };

            this.peerConnection = new RTCPeerConnection(configuration);

            if (isInitiator) {
                this.dataChannel = this.peerConnection.createDataChannel('dataChannel');
                this.setupDataChannelHandlers(this.dataChannel);
            } else {
                this.peerConnection.ondatachannel = (event) => {
                    this.dataChannel = event.channel;
                    this.setupDataChannelHandlers(this.dataChannel);
                };
            }

            this.peerConnection.onicecandidate = async (event) => {
                if (event.candidate && this.dotNetRef) {
                    await this.dotNetRef.invokeMethodAsync('SendSignalFromJS', 'IceCandidate', event.candidate);
                }
            };

            this.peerConnection.onconnectionstatechange = async () => {
                if (this.peerConnection.connectionState === 'connected') {
                    this.isConnected = true;
                    if (this.dotNetRef) {
                        await this.dotNetRef.invokeMethodAsync('HandleWebRTCConnected');
                    }
                }
            };

            this.isInitialized = true;
            return true;
        } catch (error) {
            console.error('Error initializing WebRTC:', error);
            return false;
        }
    }

    async sendMessage(message) {
        if (!this.dataChannel || this.dataChannel.readyState !== 'open' || !this.isConnected) {
            console.error('Cannot send message: Data channel not ready');
            return false;
        }

        try {
            this.dataChannel.send(message);
            return true;
        } catch (error) {
            console.error('Error sending message:', error);
            return false;
        }
    }

    dispose() {
        if (this.dataChannel) {
            this.dataChannel.close();
            this.dataChannel = null;
        }

        if (this.peerConnection) {
            this.peerConnection.close();
            this.peerConnection = null;
        }

        this.dotNetRef = null;
        this.isInitialized = false;
    }

    async createOffer() {
        if (!this.isInitialized) {
            console.error('WebRTC not initialized');
            return null;
        }

        try {
            const offer = await this.peerConnection.createOffer();
            await this.peerConnection.setLocalDescription(offer);
            return offer;
        } catch (error) {
            console.error('Error creating offer:', error);
            throw error;
        }
    }

    async handleOffer(offer, dotNetRef) {
        try {
            if (!this.isInitialized) {
                console.error('WebRTC not initialized when handling offer');
                return;
            }

            this.dotNetRef = dotNetRef;
            await this.peerConnection.setRemoteDescription(new RTCSessionDescription(offer));
            const answer = await this.peerConnection.createAnswer();
            await this.peerConnection.setLocalDescription(answer);
            await this.dotNetRef.invokeMethodAsync('SendSignalFromJS', 'Answer', answer);
        } catch (error) {
            console.error('Error handling offer:', error);
            throw error;
        }
    }

    async handleAnswer(answer) {
        if (!this.isInitialized) {
            console.error('WebRTC not initialized');
            return;
        }

        try {
            await this.peerConnection.setRemoteDescription(new RTCSessionDescription(answer));
        } catch (error) {
            console.error('Error handling answer:', error);
            throw error;
        }
    }

    async addIceCandidate(candidate) {
        try {
            await this.peerConnection.addIceCandidate(candidate);
        } catch (error) {
            console.error('Error adding ICE candidate:', error);
            throw error;
        }
    }

    getConnectionState() {
        return this.peerConnection?.connectionState || 'closed';
    }
}

window.webrtc = new WebRTCManager();