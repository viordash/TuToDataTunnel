using System.Net;
using TutoProxy.Client.Services;

namespace TutoProxy.Client.Communication {
    public interface IClientFactory {
        TcpClient CreateTcp(IPAddress localIpAddress, int port, int originPort, IClientsService clientsService, ISignalRClient dataTunnelClient);
        UdpClient CreateUdp(IPAddress localIpAddress, int port, int originPort, IClientsService clientsService, ISignalRClient dataTunnelClient);
    }
    public class ClientFactory : IClientFactory {
        readonly ILogger logger;

        public ClientFactory(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public TcpClient CreateTcp(IPAddress localIpAddress, int port, int originPort, IClientsService clientsService, ISignalRClient dataTunnelClient) {
            return new TcpClient(new IPEndPoint(localIpAddress, port), originPort, logger, clientsService, dataTunnelClient);
        }

        public UdpClient CreateUdp(IPAddress localIpAddress, int port, int originPort, IClientsService clientsService, ISignalRClient dataTunnelClient) {
            return new UdpClient(new IPEndPoint(localIpAddress, port), originPort, logger, clientsService, dataTunnelClient);
        }
    }
}
