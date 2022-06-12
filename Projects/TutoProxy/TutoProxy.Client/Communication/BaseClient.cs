using System.Net;

namespace TutoProxy.Client.Communication {

    public abstract class BaseClient : IDisposable {
        protected readonly IPEndPoint serverEndPoint;
        protected readonly ILogger logger;

        public int Port { get { return serverEndPoint.Port; } }
        public int OriginPort { get; private set; }

        public BaseClient(IPEndPoint serverEndPoint, int originPort, ILogger logger) {
            this.serverEndPoint = serverEndPoint;
            OriginPort = originPort;
            this.logger = logger;
        }

        public abstract void Dispose();
    }
}
