using System.Net;
using System.Net.Sockets;
using TuToProxy.Core;

namespace TutoProxy.Client.Communication {
    public class TcpClient : BaseClient<Socket> {
        int localPort;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        protected override TimeSpan ReceiveTimeout { get { return TcpSocketParams.ReceiveTimeout; } }

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, Action<int, int> timeoutAction)
            : base(serverEndPoint, originPort, logger, timeoutAction) {
        }

        protected override Socket CreateSocket() {
            var tcpClient = new Socket(SocketType.Stream, ProtocolType.Tcp);
            logger.Information($"tcp for server: {serverEndPoint}, o-port: {OriginPort}, created");
            return tcpClient;
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            if(!socket.Connected) {
                await socket.ConnectAsync(serverEndPoint, cancellationToken);
                localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
            }
            var txCount = await socket.SendAsync(payload, SocketFlags.None, cancellationToken);

            if(requestLogTimer <= DateTime.Now) {
                requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{txCount}");
            }
        }

        public async Task<byte[]> GetResponse(CancellationToken cancellationToken, TimeSpan timeout) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            var receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cts.Token);

            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({localPort}) response from {socket.RemoteEndPoint}, bytes:{receivedBytes}.");
            }
            return receiveBuffer[..receivedBytes].ToArray();
        }


        public override void Dispose() {
            base.Dispose();
            logger.Information($"tcp for server: {serverEndPoint}, o-port: {OriginPort}, destroyed");
        }
    }
}
