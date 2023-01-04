using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.Communication {

    public class TcpClient : BaseClient {
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        readonly Socket socket;

        Int64 totalTransmitted;
        Int64 totalReceived;

        public TcpClient(Socket socket, BaseServer tcpServer, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor)
            : base(tcpServer, ((IPEndPoint)socket.RemoteEndPoint!).Port, dataTransferService, logger, processMonitor) {

            this.socket = socket;
            socket.ReceiveBufferSize = TcpSocketParams.ReceiveBufferSize;
            socket.SendBufferSize = TcpSocketParams.ReceiveBufferSize;
            logger.Information($"{this}, created");
        }

        public override string ToString() {
            return $"tcp({base.ToString()})";
        }

        public override async ValueTask DisposeAsync() {
            cancellationTokenSource.Cancel();
            try {
                socket.Shutdown(SocketShutdown.Both);
            } catch(SocketException) { }
            try {
                await socket.DisconnectAsync(true);
            } catch(SocketException) { }
            socket.Close(100);
            processMonitor.DisconnectTcpClient(this);
            logger.Information($"{this}, disconnected, tx:{totalTransmitted}, rx:{totalReceived}");
            await base.DisposeAsync();
        }


        public async Task ReceivingStream(CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];

            processMonitor.ConnectTcpClient(this);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);

            try {
                while(socket.Connected && !cts.IsCancellationRequested) {
                    int receivedBytes;
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cts.Token);
                    if(receivedBytes == 0) {
                        break;
                    }

                    totalReceived += receivedBytes;
                    var data = receiveBuffer[..receivedBytes];

                    var transmitted = await dataTransferService.SendTcpRequest(new TcpDataRequestModel() {
                        Port = server.Port, OriginPort = OriginPort, Data = data
                    }, cancellationToken);
                    if(receivedBytes != transmitted) {
                        logger.Error($"{this} request transmit error ({transmitted})");
                        throw new SocketException((int)SocketError.ConnectionAborted);
                    }
                    if(responseLogTimer <= DateTime.Now) {
                        responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                        logger.Information($"{this} request, bytes:{data.ToShortDescriptions()}.");
                        processMonitor.TcpClientData(this, totalTransmitted, totalReceived);
                    }
                }
            } catch(OperationCanceledException) {
            } catch(SocketException ex) {
                logger.Error($"{this} rx socket ex:{ex.GetBaseException().Message}");
            } catch(Exception ex) {
                logger.Error($"{this} rx ex:{ex.GetBaseException().Message}");
            }

            if(!cancellationTokenSource.IsCancellationRequested) {
                var socketAddress = new SocketAddressModel() { Port = server.Port, OriginPort = OriginPort };
                try {
                    if(!await dataTransferService.DisconnectTcp(socketAddress, cancellationToken)) {
                        logger.Error($"{this} disconnect command error");
                    }
                } catch(Exception) { }
                await ((TcpServer)server).DisconnectAsync(socketAddress);
            }
        }

        public async ValueTask<int> SendDataAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
            try {
                var transmitted = await socket.SendAsync(payload, SocketFlags.None, cts.Token);
                if(transmitted != payload.Length) {
                    logger.Error($"{this} response transmit error ({transmitted} != {payload.Length})");
                }
                totalTransmitted += transmitted;
                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"{this} response, bytes:{payload.ToShortDescriptions()}");
                    processMonitor.TcpClientData(this, totalTransmitted, totalReceived);
                }
                return transmitted;
            } catch(SocketException ex) {
                logger.Error($"{this} send socket ex:{ex.GetBaseException().Message}");
                return -3;
            } catch(ObjectDisposedException) {
                return -2;
            } catch(Exception ex) {
                logger.Error($"{this} send ex:{ex.GetBaseException().Message}");
                return -1;
            }
        }

    }
}
