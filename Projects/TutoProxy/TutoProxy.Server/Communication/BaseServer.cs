using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Communication {
    public abstract class BaseServer : IDisposable {
        #region inner classes
        public class RemoteEndPoint {
            readonly IDateTimeService dateTimeService;
            readonly CancellationToken extCancellationToken;
            readonly Action<int> timeoutAction;
            public IPEndPoint EndPoint { get; private set; }

            CancellationTokenSource? cts = null;

            public RemoteEndPoint(IPEndPoint endPoint, IDateTimeService dateTimeService, CancellationToken extCancellationToken, Action<int> timeoutAction) {
                EndPoint = endPoint;
                this.dateTimeService = dateTimeService;
                this.timeoutAction = timeoutAction;
                this.extCancellationToken = extCancellationToken;

                StartTimeoutTimer();

            }

            public void StartTimeoutTimer() {
                cts?.Cancel();
                cts?.Dispose();

                cts = CancellationTokenSource.CreateLinkedTokenSource(extCancellationToken);
                var cancellationToken = cts.Token;

                _ = Task.Run(async () => {
                    await Task.Delay(dateTimeService.RequestTimeout, cancellationToken);
                    if(!cancellationToken.IsCancellationRequested) {
                        timeoutAction(EndPoint.Port);
                    }
                }, cancellationToken);
            }
        }
        #endregion

        protected readonly int port;
        protected readonly IPEndPoint localEndPoint;
        protected readonly IDataTransferService dataTransferService;
        protected readonly ILogger logger;
        readonly IDateTimeService dateTimeService;
        protected readonly ConcurrentDictionary<int, RemoteEndPoint> remoteEndPoints = new();

        public BaseServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IDateTimeService dateTimeService) {
            this.port = port;
            this.localEndPoint = localEndPoint;
            this.dataTransferService = dataTransferService;
            this.logger = logger;
            this.dateTimeService = dateTimeService;
        }

        public abstract void Dispose();

        protected void AddRemoteEndPoint(IPEndPoint endPoint, CancellationToken cancellationToken) {
            remoteEndPoints.AddOrUpdate(endPoint.Port,
                (k) => {
                    Debug.WriteLine($"AddRemoteEndPoint: add {k}");
                    return new RemoteEndPoint(endPoint, dateTimeService, cancellationToken, RemoveExpiredRemoteEndPoint);
                },
                (k, v) => {
                    Debug.WriteLine($"AddRemoteEndPoint: update {k}");
                    v.StartTimeoutTimer();
                    return v;
                }
            );
        }

        void RemoveExpiredRemoteEndPoint(int port) {
            Debug.WriteLine($"RemoveExpiredRemoteEndPoint: {port}");
            remoteEndPoints.TryRemove(port, out RemoteEndPoint? remoteEndPoint);
        }
    }
}
