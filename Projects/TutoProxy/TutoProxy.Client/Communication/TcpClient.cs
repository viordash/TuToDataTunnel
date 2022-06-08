using System.Net;
using System.Net.Sockets;

namespace TutoProxy.Client.Communication {
    public class TcpClient : BaseClient {
        readonly System.Net.Sockets.TcpClient tcpClient;
        readonly int localPort;

        public int Port { get { return remoteEndPoint.Port; } }

        public TcpClient(IPEndPoint remoteEndPoint, ILogger logger)
            : base(remoteEndPoint, logger) {

            //udpClient = new System.Net.Sockets.UdpClient();
            //uint IOC_IN = 0x80000000;
            //uint IOC_VENDOR = 0x18000000;
            //uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            //udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
            //udpClient.ExclusiveAddressUse = false;
            //udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            //localPort = (udpClient.Client.LocalEndPoint as IPEndPoint)!.Port;
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            var txCount = await tcpClient.SendAsync(payload, remoteEndPoint, cancellationToken);
            logger.Information($"udp({localPort}) request to {remoteEndPoint}, bytes:{txCount}");
        }

        public async Task<byte[]> GetResponse(CancellationToken cancellationToken, TimeSpan timeout) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var result = await tcpClient.ReceiveAsync(cts.Token);
            logger.Information($"udp({localPort}) response from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}.");
            return result.Buffer;
        }

        public override void Dispose() {
            tcpClient.Dispose();
        }
    }
}
