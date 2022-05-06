using System.Net;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    internal abstract class NetListener : IDisposable {
        protected readonly int port;
        protected readonly IPEndPoint localEndPoint;
        protected readonly IRequestProcessingService requestProcessingService;
        protected readonly ILogger logger;

        public NetListener(int port, IPEndPoint localEndPoint, IRequestProcessingService requestProcessingService, ILogger logger) {
            this.port = port;
            this.localEndPoint = localEndPoint;
            this.requestProcessingService = requestProcessingService;
            this.logger = logger;
        }

        public abstract void Dispose();
    }
}
