using System.Net;
using System.Timers;

namespace TutoProxy.Client.Communication {

    public abstract class BaseClient<TSocket> : IDisposable where TSocket : IDisposable {
        protected readonly IPEndPoint serverEndPoint;
        protected readonly ILogger logger;
        protected readonly TSocket socket;
        readonly Action<int, int> timeoutAction;

        protected readonly System.Timers.Timer timeoutTimer;
        protected abstract TimeSpan ReceiveTimeout { get; }

        public int Port { get { return serverEndPoint.Port; } }
        public int OriginPort { get; private set; }

        public BaseClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, Action<int, int> timeoutAction) {
            this.serverEndPoint = serverEndPoint;
            OriginPort = originPort;
            this.logger = logger;
            this.timeoutAction = timeoutAction;

            timeoutTimer = new(ReceiveTimeout.TotalMilliseconds);
            timeoutTimer.Elapsed += OnTimedEvent;
            timeoutTimer.AutoReset = false;
            timeoutTimer.Start();

            socket = CreateSocket();
        }

        void OnTimedEvent(object? source, ElapsedEventArgs e) {
            timeoutAction(Port, OriginPort);
        }

        protected abstract TSocket CreateSocket();

        public void Refresh() {
            timeoutTimer.Stop();
            timeoutTimer.Start();
        }

        public virtual void Dispose() {
            ((IDisposable)timeoutTimer).Dispose();
            ((IDisposable)socket).Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
