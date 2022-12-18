using System.Net;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    public interface IServerFactory {
        ITcpServer CreateTcp(int port, IPEndPoint localEndPoint);
        IUdpServer CreateUdp(int port, IPEndPoint localEndPoint, TimeSpan receiveTimeout);
    }
    public class ServerFactory : IServerFactory {
        readonly ILogger logger;
        readonly IDataTransferService dataTransferService;
        readonly IProcessMonitor processMonitor;

        public ServerFactory(
            ILogger logger,
            IDataTransferService dataTransferService,
            IProcessMonitor processMonitor) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(dataTransferService, nameof(dataTransferService));
            Guard.NotNull(processMonitor, nameof(processMonitor));
            this.logger = logger;
            this.dataTransferService = dataTransferService;
            this.processMonitor = processMonitor;
        }

        public ITcpServer CreateTcp(int port, IPEndPoint localEndPoint) {
            return new TcpServer(port, localEndPoint, dataTransferService, logger, processMonitor);
        }

        public IUdpServer CreateUdp(int port, IPEndPoint localEndPoint, TimeSpan receiveTimeout) {
            return new UdpServer(port, localEndPoint, dataTransferService, logger, processMonitor, receiveTimeout);
        }
    }
}
