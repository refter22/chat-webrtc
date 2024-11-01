class WebRTCManager {
    constructor() {
        this.peerConnection = null;
        this.dataChannel = null;
        this.dotNetRef = null;
        this.chunkSize = 8192;
        this.fileChunks = [];
        this.totalChunks = 0;
        this.fileMetadata = null;
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
            try {
                const data = JSON.parse(event.data);

                if (data.type === 'file-chunk') {
                    this.handleChunkReceived(
                        data.data,
                        data.index + 1,
                        data.total
                    );
                    await this.dotNetRef.invokeMethodAsync(
                        'HandleFileChunk',
                        data
                    );
                } else if (data.type === 'file-end') {
                    await this.handleFileReceived();
                    await this.dotNetRef.invokeMethodAsync('HandleFileEnd');
                } else if (data.checksum) {
                    this.fileMetadata = {
                        name: data.name,
                        size: data.size,
                        type: data.type,
                        checksum: data.checksum
                    };
                    this.fileChunks = [];
                    this.totalChunks = 0;
                    await this.dotNetRef.invokeMethodAsync(
                        'HandleFileStart',
                        data
                    );
                } else {
                    await this.dotNetRef.invokeMethodAsync(
                        'HandleWebRTCMessage',
                        event.data
                    );
                }
            } catch (error) {
                console.error('Message processing error:', error);
            }
        };
    }

    async initialize(dotNetRef, isInitiator = false) {
        try {
            this.dotNetRef = dotNetRef;

            const configuration = {
                iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
            };

            this.peerConnection = new RTCPeerConnection(configuration);

            if (isInitiator) {
                this.dataChannel =
                    this.peerConnection.createDataChannel('dataChannel');
                this.setupDataChannelHandlers(this.dataChannel);
            } else {
                this.peerConnection.ondatachannel = (event) => {
                    this.dataChannel = event.channel;
                    this.setupDataChannelHandlers(this.dataChannel);
                };
            }

            this.peerConnection.onicecandidate = async (event) => {
                if (event.candidate && this.dotNetRef) {
                    await this.dotNetRef.invokeMethodAsync(
                        'SendSignalFromJS',
                        'IceCandidate',
                        event.candidate
                    );
                }
            };

            this.peerConnection.onconnectionstatechange = async () => {
                if (this.peerConnection.connectionState === 'connected') {
                    this.isConnected = true;
                    if (this.dotNetRef) {
                        await this.dotNetRef.invokeMethodAsync(
                            'HandleWebRTCConnected'
                        );
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
        if (
            !this.dataChannel ||
            this.dataChannel.readyState !== 'open' ||
            !this.isConnected
        ) {
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
            await this.peerConnection.setRemoteDescription(
                new RTCSessionDescription(offer)
            );
            const answer = await this.peerConnection.createAnswer();
            await this.peerConnection.setLocalDescription(answer);
            await this.dotNetRef.invokeMethodAsync(
                'SendSignalFromJS',
                'Answer',
                answer
            );
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
            await this.peerConnection.setRemoteDescription(
                new RTCSessionDescription(answer)
            );
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

    async calculateChecksum(str) {
        const encoder = new TextEncoder();
        const data = encoder.encode(str);
        const hashBuffer = await crypto.subtle.digest('SHA-256', data);
        const hashArray = Array.from(new Uint8Array(hashBuffer));
        return hashArray.map((b) => b.toString(16).padStart(2, '0')).join('');
    }

    async fileToBase64(file) {
        if (!(file instanceof Blob) && !(file instanceof File)) {
            try {
                file = new Blob([file.data || file], {
                    type: file.contentType || file.type
                });
            } catch (error) {
                console.error('Error creating Blob:', error);
                throw error;
            }
        }

        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                const base64String = reader.result
                    .replace('data:', '')
                    .replace(/^.+,/, '');
                resolve(base64String);
            };
            reader.onerror = (error) => reject(error);
            reader.readAsDataURL(file);
        });
    }

    async sendFile(file) {
        try {
            const base64Data = await this.fileToBase64(file);
            const checksum = await this.calculateChecksum(base64Data);

            const metadata = {
                name: file.name,
                size: file.size,
                type: file.type,
                checksum: checksum
            };

            console.log('Sending metadata:', metadata);
            this.dataChannel.send(JSON.stringify(metadata));

            const chunks =
                base64Data.match(new RegExp(`.{1,${this.chunkSize}}`, 'g')) ||
                [];

            for (let i = 0; i < chunks.length; i++) {
                this.dataChannel.send(
                    JSON.stringify({
                        type: 'file-chunk',
                        data: chunks[i],
                        index: i,
                        total: chunks.length
                    })
                );
                console.log(`Chunk ${i + 1}/${chunks.length} sent`);
            }

            this.dataChannel.send(
                JSON.stringify({
                    type: 'file-end'
                })
            );

            console.log('File sending completed');
            return true;
        } catch (error) {
            console.error('Error sending file:', error);
            throw error;
        }
    }

    async handleFileReceived() {
        try {
            if (!this.fileMetadata) {
                throw new Error('File metadata missing');
            }

            if (
                !this.fileChunks ||
                this.fileChunks.length !== this.totalChunks
            ) {
                throw new Error(
                    `Expected ${this.totalChunks} chunks, received ${this.fileChunks?.length}`
                );
            }

            const receivedData = this.fileChunks.join('');
            const receivedChecksum = await this.calculateChecksum(receivedData);

            if (receivedChecksum !== this.fileMetadata.checksum) {
                throw new Error('Data corruption during transmission');
            }

            const dataUrl = `data:${this.fileMetadata.type};base64,${receivedData}`;
            const response = await fetch(dataUrl);
            const blob = await response.blob();
            const downloadUrl = URL.createObjectURL(blob);

            const link = document.createElement('a');
            link.href = downloadUrl;
            link.download = this.fileMetadata.name;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(downloadUrl);

            this.fileChunks = [];
            this.totalChunks = 0;
            this.fileMetadata = null;
        } catch (error) {
            console.error('Error downloading file:', error);
            this.fileChunks = [];
            this.totalChunks = 0;
            this.fileMetadata = null;
            throw error;
        }
    }

    handleChunkReceived(chunk, index, total) {
        if (!chunk) {
            console.error(`Empty chunk received: ${index}`);
            return;
        }

        if (this.totalChunks === 0) {
            this.totalChunks = total;
            this.fileChunks = new Array(total);
        }

        this.fileChunks[index - 1] = chunk;

        if (index === total) {
            const missingChunks = this.fileChunks
                .map((chunk, i) => (chunk ? null : i + 1))
                .filter((i) => i !== null);

            if (missingChunks.length > 0) {
                console.error('Missing chunks:', missingChunks);
            }
        }
    }
}

window.webrtc = new WebRTCManager();
