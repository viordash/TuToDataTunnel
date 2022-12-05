using System.Net;
using TutoProxy.Client.Services;

namespace TutoProxy.Client.Communication {

    public abstract class BaseClient : IDisposable {
        protected readonly IPEndPoint serverEndPoint;
        protected readonly ILogger logger;
        protected readonly CancellationTokenSource cancellationTokenSource;

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
            cancellationTokenSource = new CancellationTokenSource();
        }

        public virtual void Dispose() {
            cancellationTokenSource.Cancel();
            GC.SuppressFinalize(this);
        }
    }
}
