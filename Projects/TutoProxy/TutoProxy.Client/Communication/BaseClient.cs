using System.Net;
using TutoProxy.Client.Services;

namespace TutoProxy.Client.Communication {

    public abstract class BaseClient : IAsyncDisposable {
        protected readonly IPEndPoint serverEndPoint;
        protected readonly ILogger logger;
        protected readonly CancellationTokenSource cancellationTokenSource;

        protected readonly IClientsService clientsService;
        protected readonly ISignalRClient dataTunnelClient;
        protected readonly IProcessMonitor processMonitor;

        public int Port { get { return serverEndPoint.Port; } }
        public int OriginPort { get; private set; }

        public BaseClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient, IProcessMonitor processMonitor) {
            this.serverEndPoint = serverEndPoint;
            OriginPort = originPort;
            this.logger = logger;
            this.clientsService = clientsService;
            this.dataTunnelClient = dataTunnelClient;
            cancellationTokenSource = new CancellationTokenSource();
            this.processMonitor = processMonitor;
        }

        public virtual ValueTask DisposeAsync() {
            cancellationTokenSource.Cancel();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
