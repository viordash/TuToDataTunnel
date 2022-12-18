using System.Net;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    public interface IServerFactory {
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

        public IUdpServer CreateUdp(int port, IPEndPoint localEndPoint, TimeSpan receiveTimeout) {
            return new UdpServer(port, localEndPoint, dataTransferService, logger, processMonitor, receiveTimeout);
        }
    }
}
