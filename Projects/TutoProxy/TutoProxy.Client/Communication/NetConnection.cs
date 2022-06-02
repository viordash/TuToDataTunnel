using System.Net;

namespace TutoProxy.Client.Communication {

    public abstract class NetConnection : IDisposable {
        protected readonly IPEndPoint remoteEndPoint;
        protected readonly ILogger logger;

        public NetConnection(IPEndPoint remoteEndPoint, ILogger logger) {
            this.remoteEndPoint = remoteEndPoint;
            this.logger = logger;
        }

        public abstract void Dispose();
    }
}
