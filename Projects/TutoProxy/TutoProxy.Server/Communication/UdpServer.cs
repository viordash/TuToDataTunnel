using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    internal class UdpServer : BaseServer {
        readonly UdpClient udpServer;
        readonly CancellationTokenSource cts;
        readonly CancellationToken cancellationToken;

        protected readonly ConcurrentDictionary<int, IPEndPoint> remoteEndPoints = new();

        public UdpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger)
            : base(port, localEndPoint, dataTransferService, logger) {
            udpServer = new UdpClient(port);
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            cts = new CancellationTokenSource();
            cancellationToken = cts.Token;
        }

        public Task Listen() {
            return Task.Run(async () => {
                while(!cancellationToken.IsCancellationRequested) {
                    var result = await udpServer.ReceiveAsync(cancellationToken);
                    logger.Information($"udp request from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}");
                    await dataTransferService.SendUdpRequest(new UdpDataRequestModel(port, result.RemoteEndPoint.Port, result.Buffer));
                    remoteEndPoints.TryAdd(result.RemoteEndPoint.Port, result.RemoteEndPoint);
                }
            }, cancellationToken);
        }

        public async Task SendResponse(UdpDataResponseModel response) {
            if(cancellationToken.IsCancellationRequested) {
                return;
            }
            if(!remoteEndPoints.TryGetValue(response.RemotePort, out IPEndPoint? remoteEndPoint)) {
                return;
            }
            var txCount = await udpServer.SendAsync(response.Data, remoteEndPoint, cancellationToken);
            logger.Information($"udp response to {remoteEndPoint}, bytes:{txCount}");
        }

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
            udpServer.Dispose();
        }
    }
}
