using System.Net;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    public abstract class BaseServer : IDisposable {

        protected readonly int port;
        protected readonly IPEndPoint localEndPoint;
        protected readonly IDataTransferService dataTransferService;
        protected readonly HubClient hubClient;
        protected readonly ILogger logger;

        public BaseServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, HubClient hubClient, ILogger logger) {
            this.port = port;
            this.localEndPoint = localEndPoint;
            this.dataTransferService = dataTransferService;
            this.hubClient = hubClient;
            this.logger = logger;
        }

        public abstract void Dispose();


    }
}
