using System.Net;
using System.Net.Sockets;
using TuToProxy.Core;

namespace TutoProxy.Client.Communication {
    public class TcpClient : BaseClient {
        readonly Socket tcpClient;
        int localPort;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        public int Port { get { return remoteEndPoint.Port; } }

        public TcpClient(IPEndPoint remoteEndPoint, ILogger logger)
            : base(remoteEndPoint, logger) {

            tcpClient = new Socket(SocketType.Stream, ProtocolType.Tcp);
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            if(!tcpClient.Connected) {
                await tcpClient.ConnectAsync(remoteEndPoint, cancellationToken);
                localPort = (tcpClient.LocalEndPoint as IPEndPoint)!.Port;
            }
            var txCount = await tcpClient.SendAsync(payload, SocketFlags.None, cancellationToken);

            if(requestLogTimer <= DateTime.Now) {
                requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({localPort}) request to {remoteEndPoint}, bytes:{txCount}");
            }
        }

        public async Task<byte[]> GetResponse(CancellationToken cancellationToken, TimeSpan timeout) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];
            var receivedBytes = await tcpClient.ReceiveAsync(receiveBuffer, SocketFlags.None, cts.Token);

            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                logger.Information($"tcp({localPort}) response from {tcpClient.RemoteEndPoint}, bytes:{receivedBytes}.");
            }
            return receiveBuffer[..receivedBytes].ToArray();
        }

        public override void Dispose() {
            tcpClient.Dispose();
        }
    }
}
