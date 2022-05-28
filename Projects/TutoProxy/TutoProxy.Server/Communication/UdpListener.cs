using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    internal class UdpListener : NetListener {
        readonly UdpClient udpServer;
        readonly CancellationTokenSource cts;

        public UdpListener(int port, IPEndPoint localEndPoint, IRequestProcessingService requestProcessingService, ILogger logger)
            : base(port, localEndPoint, requestProcessingService, logger) {
            udpServer = new UdpClient(port);
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            cts = new CancellationTokenSource();
        }

        public Task Listen() {
            var cancellationToken = cts.Token;
            return Task.Run(async () => {
                while(!cancellationToken.IsCancellationRequested) {
                    var result = await udpServer.ReceiveAsync(cancellationToken);
                    logger.Information($"udp request from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}");
                    var response = await requestProcessingService.UdpRequest(new UdpDataRequestModel() {
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
            cts.Cancel();
            cts.Dispose();
            udpServer.Dispose();
        }
    }
}
