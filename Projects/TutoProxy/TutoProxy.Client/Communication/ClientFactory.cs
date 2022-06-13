using System.Net;

namespace TutoProxy.Client.Communication {
    public interface IClientFactory {
        TcpClient Create(IPAddress localIpAddress, TcpDataRequestModel request, Action<int, int> timeoutAction);
        UdpClient Create(IPAddress localIpAddress, UdpDataRequestModel request, Action<int, int> timeoutAction);
    }
    public class ClientFactory : IClientFactory {
        readonly ILogger logger;

        public ClientFactory(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public TcpClient Create(IPAddress localIpAddress, TcpDataRequestModel request, Action<int, int> timeoutAction) {
            throw new NotImplementedException();
        }

        public UdpClient Create(IPAddress localIpAddress, UdpDataRequestModel request, Action<int, int> timeoutAction) {
            return new UdpClient(new IPEndPoint(localIpAddress, request.Port), request.OriginPort, logger, timeoutAction);
        }
    }
}
