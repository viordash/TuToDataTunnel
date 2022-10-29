using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Client.Communication {
    public class TcpClient : BaseClient<Socket> {
        int localPort;
        DateTime requestLogTimer = DateTime.Now;

        protected override TimeSpan ReceiveTimeout { get { return TcpSocketParams.ReceiveTimeout; } }

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, CancellationTokenSource cancellationTokenSource)
            : base(serverEndPoint, originPort, logger, clientsService, cancellationTokenSource) {
        }

        protected override void OnTimedEvent(object? state) {
            clientsService.RemoveTcpClient(Port, OriginPort);
        }

        protected override Socket CreateSocket() {
            var tcpClient = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            //tcpClient.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            //tcpClient.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
            //tcpClient.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);

            logger.Information($"tcp for server: {serverEndPoint}, o-port: {OriginPort}, created");
            return tcpClient;
        }

        public override void Dispose() {
            base.Dispose();
            logger.Information($"tcp for server: {serverEndPoint}, o-port: {OriginPort}, destroyed");
        }

        public async Task CreateStream(TcpStreamParam streamParam, IAsyncEnumerable<byte[]> stream, ISignalRClient dataTunnelClient) {
            if(!socket.Connected) {
                await socket.ConnectAsync(serverEndPoint, cancellationTokenSource.Token);
                localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            }

            await dataTunnelClient.CreateStream(streamParam, ClientStreamData(cancellationTokenSource), cancellationTokenSource.Token);

            int totalBytes = 0;
            await foreach(var data in stream.WithCancellation(cancellationTokenSource.Token)) {
                if(!socket.Connected) {
                    logger.Information($"tcp({localPort}) request to {serverEndPoint}, ---- {cancellationTokenSource.Token.IsCancellationRequested}");
                    break;
                }
                await socket.SendAsync(data, SocketFlags.None, cancellationTokenSource.Token);

                totalBytes += data.Length;
                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{data?.ToShortDescriptions()}");
                }
            }
            cancellationTokenSource.Cancel();
            logger.Information($"tcp({localPort}) request to {serverEndPoint} completed, transfered {totalBytes} b");
            clientsService.RemoveTcpClient(Port, OriginPort);
        }

        async IAsyncEnumerable<byte[]> ClientStreamData(CancellationTokenSource cts, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            int receivedBytes;
            int totalBytes = 0;
            while(socket.Connected && !cancellationToken.IsCancellationRequested) {
                try {
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                    if(receivedBytes == 0) {
                        break;
                    }
                } catch(OperationCanceledException) {
                    break;
                } catch(SocketException) {
                    break;
                }
                totalBytes += receivedBytes;
                var data = receiveBuffer[..receivedBytes].ToArray();
                yield return data;

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) response from {serverEndPoint}, bytes:{data.ToShortDescriptions()}.");
                }
            }
            logger.Information($"tcp({localPort}) disconnected, received {totalBytes} b");
            socket.Close();
            cts.Cancel();
            clientsService.RemoveTcpClient(Port, OriginPort);
        }
    }
}
