using System.Net;

namespace TutoProxy.Client.Communication {

    abstract class NetClient : IDisposable {
        protected readonly IPEndPoint remoteEndPoint;
        protected readonly ILogger logger;

        public NetClient(IPEndPoint remoteEndPoint, ILogger logger) {
            this.remoteEndPoint = remoteEndPoint;
            this.logger = logger;
        }

        public abstract void Dispose();
    }
}
