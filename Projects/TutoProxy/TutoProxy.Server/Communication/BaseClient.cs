using System.Threading;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {

    public abstract class BaseClient : IAsyncDisposable {
        protected readonly IDataTransferService dataTransferService;
        protected readonly ILogger logger;
        protected readonly CancellationTokenSource cancellationTokenSource;
        protected readonly IProcessMonitor processMonitor;
        protected readonly BaseServer server;

        public int Port { get { return server.Port; } }
        public int OriginPort { get; private set; }

        public BaseClient(BaseServer server, int originPort, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor) {
            this.server = server;
            OriginPort = originPort;
            this.dataTransferService = dataTransferService;
            this.logger = logger;
            cancellationTokenSource = new CancellationTokenSource();
            this.processMonitor = processMonitor;
        }

        public virtual ValueTask DisposeAsync() {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        public override string ToString() {
            return $"{OriginPort,5}";
        }
    }
}
