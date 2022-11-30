using System.Net;
using TutoProxy.Client.Services;

namespace TutoProxy.Client.Communication {

    public abstract class BaseClient {
        protected readonly IPEndPoint serverEndPoint;
        protected readonly ILogger logger;

        protected readonly IClientsService clientsService;
        protected readonly ISignalRClient dataTunnelClient;

        public int Port { get { return serverEndPoint.Port; } }
        public int OriginPort { get; private set; }

        public BaseClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient) {
            this.serverEndPoint = serverEndPoint;
            OriginPort = originPort;
            this.logger = logger;
            this.clientsService = clientsService;
            this.dataTunnelClient = dataTunnelClient;
        }
    }
}
