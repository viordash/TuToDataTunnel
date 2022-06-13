using System.Net;
using System.Net.Sockets;
using TuToProxy.Core;

namespace TutoProxy.Client.Communication {
    public class UdpClient : BaseClient<System.Net.Sockets.UdpClient> {
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        protected override TimeSpan ReceiveTimeout { get { return UdpSocketParams.ReceiveTimeout; } }

        public UdpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, Action<int, int> timeoutAction)
            : base(serverEndPoint, originPort, logger, timeoutAction) {
        }

        protected override System.Net.Sockets.UdpClient CreateSocket() {
            var udpClient = new System.Net.Sockets.UdpClient(serverEndPoint.AddressFamily);
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
            udpClient.ExclusiveAddressUse = false;
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            logger.Information($"udp for server: {serverEndPoint}, o-port: {OriginPort}, created");
            return udpClient;
        }

        public async Task SendRequest(byte[] payload, CancellationToken cancellationToken) {
            var txCount = await socket!.SendAsync(payload, serverEndPoint, cancellationToken);
            if(requestLogTimer <= DateTime.Now) {
                requestLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                logger.Information($"udp({(socket.Client.LocalEndPoint as IPEndPoint)!.Port}) request to {serverEndPoint}, bytes:{txCount}");
            }
        }

        public async Task<byte[]> GetResponse(CancellationToken cancellationToken, TimeSpan timeout) {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var result = await socket.ReceiveAsync(cts.Token);

            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                logger.Information($"udp({(socket.Client.LocalEndPoint as IPEndPoint)!.Port}) response from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}.");

            }
            return result.Buffer;
        }

        public override void Dispose() {
            base.Dispose();
            logger.Information($"udp for server: {serverEndPoint}, o-port: {OriginPort}, destroyed");
        }
    }
}
