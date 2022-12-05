using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Client.Communication {

    public class TcpClient : BaseClient {
        int? localPort = null;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        readonly Socket socket;

        Int64 totalTransmitted;
        Int64 totalReceived;

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {

            socket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, created");
        }

        ValueTask<bool> TryShutdown() {
            return clientsService.RemoveTcpClient(Port, OriginPort);
        }

        public override async ValueTask DisposeAsync() {
            await base.DisposeAsync();
            try {
                socket.Shutdown(SocketShutdown.Both);
            } catch(SocketException) { }
            try {
                await socket.DisconnectAsync(true);
            } catch(SocketException) { }
            socket.Close(100);
            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, destroyed, tx:{totalTransmitted}, rx:{totalReceived}");
        }

        public async ValueTask<bool> Connect(CancellationToken cancellationToken) {
            if(socket.Connected) {
                logger.Error($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, already connected");
                return false;
            }

            try {
                await socket.ConnectAsync(serverEndPoint);
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
                return false;
            }
            localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            _ = Task.Run(() => ReceivingStream(cancellationToken), cancellationToken);
            return true;
        }

        async Task ReceivingStream(CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);

            while(socket.Connected && !cts.IsCancellationRequested) {
                int receivedBytes;
                try {
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cts.Token);
                    if(receivedBytes == 0) {
                        break;
                    }
                } catch(OperationCanceledException ex) {
                    break;
                } catch(SocketException ex) {
                    logger.Error(ex.GetBaseException().Message);
                    break;
                } catch(Exception ex) {
                    logger.Error(ex.GetBaseException().Message);
                    break;
                }
                totalReceived += receivedBytes;
                var data = receiveBuffer[..receivedBytes].ToArray();

                var response = new TcpDataResponseModel() { Port = Port, OriginPort = OriginPort, Data = data };
                var transmitted = await dataTunnelClient.SendTcpResponse(response, cts.Token);
                if(receivedBytes != transmitted) {
                    logger.Error($"tcp({localPort}) response from {serverEndPoint} send error ({transmitted})");
                }
                if(responseLogTimer <= DateTime.Now) {
                    responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) response from {serverEndPoint}, bytes:{data.ToShortDescriptions()}.");
                }
            }
            if(!cancellationTokenSource.IsCancellationRequested) {
                if(!await dataTunnelClient.DisconnectTcp(new SocketAddressModel() { Port = Port, OriginPort = OriginPort }, cancellationToken)) {
                    logger.Error($"tcp({localPort}) response from {serverEndPoint} disconnect error");
                }
                await TryShutdown();
            }
        }

        public async ValueTask<int> SendRequest(byte[] payload, CancellationToken cancellationToken) {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
            try {
                var transmitted = await socket.SendAsync(payload, SocketFlags.None, cts.Token);

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{payload.ToShortDescriptions()}");
                }
                return transmitted;
            } catch(SocketException) {
                return -3;
            } catch(ObjectDisposedException) {
                return -2;
            } catch(Exception ex) {
                logger.Error(ex.GetBaseException().Message);
                return -1;
            }
        }

        public ValueTask<bool> DisconnectAsync(CancellationToken cancellationToken) {
            return TryShutdown();
        }

    }
}
