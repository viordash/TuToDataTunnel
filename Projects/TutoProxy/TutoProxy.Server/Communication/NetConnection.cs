using System.Net;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    internal abstract class NetConnection : IDisposable {
        protected readonly int port;
        protected readonly IPEndPoint localEndPoint;
        protected readonly IDataTransferService dataTransferService;
        protected readonly ILogger logger;

        public NetConnection(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger) {
            this.port = port;
            this.localEndPoint = localEndPoint;
            this.dataTransferService = dataTransferService;
            this.logger = logger;
        }

        public abstract void Dispose();
    }
}
