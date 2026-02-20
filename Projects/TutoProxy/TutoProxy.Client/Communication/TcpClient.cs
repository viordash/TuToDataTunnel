using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;
using TuToProxy.Core.Pipeline;

namespace TutoProxy.Client.Communication {

    public class TcpClient : BaseClient {
        int? localPort = null;
        long requestLogTicks = Environment.TickCount64;
        long responseLogTicks = Environment.TickCount64;
        readonly Socket socket;

        Int64 totalTransmitted;
        Int64 totalReceived;

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient, IProcessMonitor processMonitor)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient, processMonitor) {

            socket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;
            socket.ReceiveBufferSize = TcpSocketParams.ReceiveBufferSize;
            socket.SendBufferSize = TcpSocketParams.ReceiveBufferSize;
            logger.Information($"{this}, created");
        }

        public override string ToString() {
            return $"tcp({localPort,5}) {base.ToString()}";
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
            logger.Information($"{this}, destroyed, tx:{totalTransmitted}, rx:{totalReceived}");
            await base.DisposeAsync();
        }

        async ValueTask<SocketError> ConnectInternal(CancellationToken cancellationToken, int nestedLevel) {
            if(socket.Connected) {
                logger.Error($"{this}, already connected");
                return SocketError.Success;
            }

            try {
                await socket.ConnectAsync(serverEndPoint);
                processMonitor.ConnectTcpClient(this);
            } catch(SocketException ex) {
                logger.Error($"{this}, connect socket ex: {ex.GetBaseException().Message}");
                return ex.SocketErrorCode;
            } catch(Exception ex) {
                logger.Error($"{this}, connect ex: {ex.GetBaseException().Message}");
                return SocketError.SocketError;
            }
            localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            _ = Task.Run(async () => await ReceivingStream(cancellationToken), cancellationToken);
            return SocketError.Success;
        }

        public ValueTask<SocketError> Connect(CancellationToken cancellationToken) {
            return ConnectInternal(cancellationToken, 0);
        }

        async Task ReceivingStream(CancellationToken cancellationToken) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);

            await using var pipeline = new TcpPipeline<TcpDataResponseModel>(socket);
            var hasError = false;

            try {
                var readerTask = pipeline.ReadFromSocket(bytes => totalReceived += bytes, cts.Token);
                var processorTask = pipeline.ProcessPipe(Port, OriginPort, cts.Token);
                var senderTask = pipeline.SendRequests(
                    async (response, ct) => await dataTunnelClient.SendTcpResponse(response, ct),
                    (seq, transmitted) => {
                        totalTransmitted += transmitted;
                        if(TcpSocketParams.TrafficMonitoring && Environment.TickCount64 - responseLogTicks >= TcpSocketParams.LogUpdatePeriod * 1000) {
                            responseLogTicks = Environment.TickCount64;
                            logger.Information($"{this} response seq:{seq}, transmitted:{transmitted}");
                            processMonitor.TcpClientData(this, totalTransmitted, totalReceived);
                        }
                    },
                    (seq, expected, actual) => {
                        logger.Error($"{this} response seq:{seq} transmit error ({actual} != {expected})");
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
                try {
                    if(!await dataTunnelClient.DisconnectTcp(new SocketAddressModel() { Port = Port, OriginPort = OriginPort }, cancellationToken)) {
                        logger.Error($"{this} disconnect command error");
                    }
                } catch(Exception) { }
                await DisconnectAsync();
            }
        }

        public async ValueTask<int> SendRequest(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
            try {
                var transmitted = await socket.SendAsync(payload, SocketFlags.None, cts.Token);
                if(transmitted != payload.Length) {
                    logger.Error($"{this} request transmit error ({transmitted} != {payload.Length})");
                }
                totalTransmitted += transmitted;
                if(TcpSocketParams.TrafficMonitoring && Environment.TickCount64 - requestLogTicks >= TcpSocketParams.LogUpdatePeriod * 1000) {
                    requestLogTicks = Environment.TickCount64;
                    logger.Information($"{this} request, bytes:{payload.ToShortDescriptions()}");
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

        public ValueTask<bool> DisconnectAsync() {
            return clientsService.RemoveTcpClient(Port, OriginPort);
        }

    }
}
