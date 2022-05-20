using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    internal class UdpListener : NetListener {
        readonly UdpClient udpServer;

        public UdpListener(int port, IPEndPoint localEndPoint, IRequestProcessingService requestProcessingService, ILogger logger)
            : base(port, localEndPoint, requestProcessingService, logger) {
            udpServer = new UdpClient(port);
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public Task Listen(CancellationToken cancellationToken) {
            return Task.Run(async () => {
                while(!cancellationToken.IsCancellationRequested) {
                    var result = await udpServer.ReceiveAsync(cancellationToken);
                    logger.Information($"udp request from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}");
                    var response = await requestProcessingService.Request(new UdpDataRequestModel() {
                        Data = result.Buffer
                    });

                    using var udpClient = new UdpClient(localEndPoint);
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    var txCount = await udpClient.SendAsync(response.Data, result.RemoteEndPoint, cancellationToken);
                    logger.Information($"udp response to {result.RemoteEndPoint}, bytes:{txCount}");
                }
            }, cancellationToken);
        }

        public override void Dispose() {
            udpServer.Dispose();
        }
    }
}
