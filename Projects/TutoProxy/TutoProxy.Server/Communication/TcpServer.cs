using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Communication {
    internal class TcpServer : BaseServer {
        readonly TcpListener tcpServer;
        readonly CancellationTokenSource cts;
        readonly CancellationToken cancellationToken;

        IPEndPoint? remoteEndPoint = null;

        public TcpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IDateTimeService dateTimeService)
            : base(port, localEndPoint, dataTransferService, logger, dateTimeService) {
            tcpServer = new TcpListener(IPAddress.Loopback, port);

            cts = new CancellationTokenSource();
            cancellationToken = cts.Token;
        }

        public Task Listen() {
            return Task.Run(async () => {

                tcpServer.Start();
                while(!cancellationToken.IsCancellationRequested) {

                    var socket = tcpServer.AcceptSocket();

                    logger.Information($"tcp accept {socket}");
                    _ = Task.Run(async () => await HandleSocket(socket, cancellationToken));


                    //var result = await tcpServer.ReceiveAsync(cancellationToken);
                    //await dataTransferService.SendUdpRequest(new UdpDataRequestModel(port, result.Buffer));
                    //remoteEndPoint = result.RemoteEndPoint;
                }
            }, cancellationToken);
        }

        async Task HandleSocket(Socket socket, CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[8192];
            while(socket.Connected) {
                var receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                await dataTransferService.SendTcpRequest(new TcpDataRequestModel(port, ((IPEndPoint)socket.RemoteEndPoint!).Port, receiveBuffer.Slice(0, receivedBytes).ToArray()));


                //// Blocks until send returns.
                //int byteCount = server.Send(msg, SocketFlags.None);
                //Console.WriteLine("Sent {0} bytes.", byteCount);

                //// Get reply from the server.
                //byteCount = server.Receive(bytes, SocketFlags.None);
                //if(byteCount > 0)
                //    Console.WriteLine(Encoding.UTF8.GetString(bytes));

            }
        }

        public async Task SendResponse(TcpDataResponseModel response) {
            if(cancellationToken.IsCancellationRequested || remoteEndPoint == null) {
                return;
            }
            //var txCount = await tcpServer.SendAsync(response.Data, remoteEndPoint, cancellationToken);
            //logger.Information($"tcp response to {remoteEndPoint}, bytes:{txCount}");
        }

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
