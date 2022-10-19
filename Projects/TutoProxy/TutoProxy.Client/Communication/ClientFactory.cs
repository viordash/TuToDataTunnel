using System.Net;

namespace TutoProxy.Client.Communication {
    public interface IClientFactory {
        TcpClient CreateTcp(IPAddress localIpAddress, int port, int originPort, Action<int, int> timeoutAction);
        UdpClient CreateUdp(IPAddress localIpAddress, int port, int originPort, Action<int, int> timeoutAction);
    }
    public class ClientFactory : IClientFactory {
        readonly ILogger logger;

        public ClientFactory(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public TcpClient CreateTcp(IPAddress localIpAddress, int port, int originPort, Action<int, int> timeoutAction) {
            return new TcpClient(new IPEndPoint(localIpAddress, port), originPort, logger, timeoutAction);
        }

        public UdpClient CreateUdp(IPAddress localIpAddress, int port, int originPort, Action<int, int> timeoutAction) {
            return new UdpClient(new IPEndPoint(localIpAddress, port), originPort, logger, timeoutAction);
        }
    }
}
