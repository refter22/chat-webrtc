window.webrtc = {
    async createConnection(dotNetRef, targetUserId) {
        const pc = new RTCPeerConnection({
            iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
        });

        pc.onicecandidate = (event) => {
            if (event.candidate) {
                dotNetRef.invokeMethodAsync(
                    'HandleIceCandidate',
                    targetUserId,
                    event.candidate
                );
            }
        };

        // Обработка входящего канала данных
        pc.ondatachannel = (event) => {
            const channel = event.channel;
            setupDataChannel(channel);
        };

        // Создаем исходящий канал данных
        const channel = pc.createDataChannel('dataChannel');
        setupDataChannel(channel);

        function setupDataChannel(channel) {
            channel.onopen = () => {
                dotNetRef.invokeMethodAsync(
                    'HandleConnectionOpened',
                    targetUserId
                );
            };

            channel.onclose = () => {
                dotNetRef.invokeMethodAsync(
                    'HandleConnectionClosed',
                    targetUserId
                );
            };

            channel.onmessage = (event) => {
                dotNetRef.invokeMethodAsync(
                    'HandleDataReceived',
                    targetUserId,
                    event.data
                );
            };
        }

        return { pc, channel };
    },

    async createOffer(connection) {
        const offer = await connection.pc.createOffer();
        await connection.pc.setLocalDescription(offer);
        return offer;
    },

    async handleOffer(connection, offer) {
        await connection.pc.setRemoteDescription(
            new RTCSessionDescription(offer)
        );
        const answer = await connection.pc.createAnswer();
        await connection.pc.setLocalDescription(answer);
        return answer;
    },

    async handleAnswer(connection, answer) {
        await connection.pc.setRemoteDescription(
            new RTCSessionDescription(answer)
        );
    },

    async addIceCandidate(connection, candidate) {
        await connection.pc.addIceCandidate(new RTCIceCandidate(candidate));
    },

    async sendData(connection, data) {
        if (connection?.channel?.readyState === 'open') {
            connection.channel.send(data);
            return true;
        }
        console.warn(connection?.channel?.readyState);
        return false;
    },

    closeConnection(connection) {
        if (connection?.channel) {
            connection.channel.close();
        }
        if (connection?.pc) {
            connection.pc.close();
        }
    }
};
