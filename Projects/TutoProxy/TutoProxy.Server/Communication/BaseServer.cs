using System.Net;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    public abstract class BaseServer : IDisposable {

        public int Port { get; private set; }
        protected readonly IPEndPoint localEndPoint;
        protected readonly IDataTransferService dataTransferService;
        protected readonly IProcessMonitor processMonitor;
        protected readonly ILogger logger;

        public BaseServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor) {
            this.Port = port;
            this.localEndPoint = localEndPoint;
            this.dataTransferService = dataTransferService;
            this.logger = logger;
            this.processMonitor = processMonitor;
        }

        public abstract void Dispose();


    }
}
