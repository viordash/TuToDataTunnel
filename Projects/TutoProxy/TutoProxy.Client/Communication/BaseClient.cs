using System.Net;

namespace TutoProxy.Client.Communication {

    public abstract class BaseClient : IDisposable {
        protected readonly IPEndPoint remoteEndPoint;
        protected readonly ILogger logger;

        public BaseClient(IPEndPoint remoteEndPoint, ILogger logger) {
            this.remoteEndPoint = remoteEndPoint;
            this.logger = logger;
        }

        public abstract void Dispose();
    }
}
