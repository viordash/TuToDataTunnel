using System.Net;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Communication {
    public abstract class BaseServer : IDisposable {

        protected readonly int port;
        protected readonly IPEndPoint localEndPoint;
        protected readonly IDataTransferService dataTransferService;
        protected readonly ILogger logger;
        protected readonly IDateTimeService dateTimeService;

        public BaseServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IDateTimeService dateTimeService) {
            this.port = port;
            this.localEndPoint = localEndPoint;
            this.dataTransferService = dataTransferService;
            this.logger = logger;
            this.dateTimeService = dateTimeService;
        }

        public abstract void Dispose();


    }
}
