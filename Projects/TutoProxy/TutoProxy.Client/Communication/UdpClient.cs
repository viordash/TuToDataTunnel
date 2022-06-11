using System.Net;
using System.Net.Sockets;

namespace TutoProxy.Client.Communication {
    public class UdpClient : BaseClient {
        readonly System.Net.Sockets.UdpClient udpClient;
        readonly int localPort;

        public int Port { get { return remoteEndPoint.Port; } }

        public UdpClient(IPEndPoint remoteEndPoint, ILogger logger)
            : base(remoteEndPoint, logger) {

            udpClient = new System.Net.Sockets.UdpClient(remoteEndPoint.AddressFamily);
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
            udpClient.ExclusiveAddressUse = false;
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            var txCount = await udpClient.SendAsync(payload, remoteEndPoint, cancellationToken);
            logger.Information($"udp({(udpClient.Client.LocalEndPoint as IPEndPoint)!.Port}) request to {remoteEndPoint}, bytes:{txCount}");
        }

        public async Task<byte[]> GetResponse(CancellationToken cancellationToken, TimeSpan timeout) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var result = await udpClient.ReceiveAsync(cts.Token);
            logger.Information($"udp({(udpClient.Client.LocalEndPoint as IPEndPoint)!.Port}) response from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}.");
            return result.Buffer;
        }

        public override void Dispose() {
            udpClient.Dispose();
        }
    }
}
