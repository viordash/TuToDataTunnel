using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;
using TuToProxy.Core.Pipeline;

namespace TutoProxy.Server.Communication {

    public class TcpClient : BaseClient {
        long requestLogTicks = Environment.TickCount64;
        long responseLogTicks = Environment.TickCount64;
        readonly Socket socket;

        Int64 totalTransmitted;
        Int64 totalReceived;

        public TcpClient(Socket socket, BaseServer tcpServer, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor)
            : base(tcpServer, ((IPEndPoint)socket.RemoteEndPoint!).Port, dataTransferService, logger, processMonitor) {

            this.socket = socket;
            socket.NoDelay = true;
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
            processMonitor.ConnectTcpClient(this);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);

            await using var pipeline = new TcpPipeline<TcpDataRequestModel>(socket);
            var hasError = false;

            try {
                var readerTask = pipeline.ReadFromSocket(bytes => totalReceived += bytes, cts.Token);
                var processorTask = pipeline.ProcessPipe(server.Port, OriginPort, cts.Token);
                var senderTask = pipeline.SendRequests(
                    async (request, ct) => await dataTransferService.SendTcpRequest(request, ct),
                    (seq, transmitted) => {
                        totalTransmitted += transmitted;
                        if(TcpSocketParams.TrafficMonitoring && Environment.TickCount64 - responseLogTicks >= TcpSocketParams.LogUpdatePeriod * 1000) {
                            responseLogTicks = Environment.TickCount64;
                            logger.Information($"{this} request seq:{seq}, transmitted:{transmitted}");
                            processMonitor.TcpClientData(this, totalTransmitted, totalReceived);
                        }
                    },
                    (seq, expected, actual) => {
                        logger.Error($"{this} request seq:{seq} transmit error ({actual} != {expected})");
                        hasError = true;
                        cancellationTokenSource.Cancel();
                    },
                    cts.Token
                );

                await Task.WhenAll(readerTask, processorTask, senderTask);
            } catch(OperationCanceledException) {
            } catch(SocketException ex) {
                logger.Error($"{this} rx socket ex:{ex.GetBaseException().Message}");
            } catch(Exception ex) {
                logger.Error($"{this} rx ex:{ex.GetBaseException().Message}");
            }

            if(!cancellationTokenSource.IsCancellationRequested || hasError) {
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
                if(TcpSocketParams.TrafficMonitoring && Environment.TickCount64 - requestLogTicks >= TcpSocketParams.LogUpdatePeriod * 1000) {
                    requestLogTicks = Environment.TickCount64;
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
