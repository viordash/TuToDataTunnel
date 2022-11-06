using System.Net;
using TutoProxy.Client.Services;

namespace TutoProxy.Client.Communication {

    public abstract class BaseClient<TSocket> : IDisposable where TSocket : IDisposable {
        protected readonly IPEndPoint serverEndPoint;
        protected readonly ILogger logger;
        protected readonly TSocket socket;

        protected readonly Timer timeoutTimer;
        protected abstract TimeSpan ReceiveTimeout { get; }

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

            timeoutTimer = new(OnTimedEvent, null, ReceiveTimeout, Timeout.InfiniteTimeSpan);

            socket = CreateSocket();
        }

        protected abstract void OnTimedEvent(object? state);

        protected abstract TSocket CreateSocket();

        public void Refresh() {
            if(!timeoutTimer.Change(ReceiveTimeout, Timeout.InfiniteTimeSpan)) {
                logger.Error(
                    this switch {
                        TcpClient => $"tcp: {serverEndPoint}, o-port: {OriginPort}, Refresh error",
                        UdpClient => $"udp: {serverEndPoint}, o-port: {OriginPort}, Refresh error",
                        _ => $"???: {serverEndPoint}, o-port: {OriginPort}, Refresh error",
                    }
                );
            }
        }

        public virtual void Dispose() {
            ((IDisposable)timeoutTimer).Dispose();
            ((IDisposable)socket).Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
