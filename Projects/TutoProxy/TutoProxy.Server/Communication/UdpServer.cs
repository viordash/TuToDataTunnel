using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Communication {
    public class UdpServer : BaseServer {
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

        readonly UdpClient udpServer;
        readonly CancellationTokenSource cts;
        readonly CancellationToken cancellationToken;

        protected readonly ConcurrentDictionary<int, RemoteEndPoint> remoteEndPoints = new();

        public UdpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IDateTimeService dateTimeService)
            : base(port, localEndPoint, dataTransferService, logger, dateTimeService) {
            udpServer = new UdpClient(port);
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            cts = new CancellationTokenSource();
            cancellationToken = cts.Token;
        }

        public Task Listen() {
            return Task.Run(async () => {
                while(!cancellationToken.IsCancellationRequested) {
                    var result = await udpServer.ReceiveAsync(cancellationToken);
                    logger.Information($"udp request from {result.RemoteEndPoint}, bytes:{result.Buffer.Length}");
                    await dataTransferService.SendUdpRequest(new UdpDataRequestModel(port, result.RemoteEndPoint.Port, result.Buffer));
                    AddRemoteEndPoint(result.RemoteEndPoint, cancellationToken);
                }
            }, cancellationToken);
        }

        public async Task SendResponse(UdpDataResponseModel response) {
            if(cancellationToken.IsCancellationRequested) {
                return;
            }
            if(!remoteEndPoints.TryGetValue(response.RemotePort, out RemoteEndPoint? remoteEndPoint)) {
                return;
            }
            var txCount = await udpServer.SendAsync(response.Data, remoteEndPoint.EndPoint, cancellationToken);
            logger.Information($"udp response to {remoteEndPoint}, bytes:{txCount}");
        }

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
            udpServer.Dispose();
        }

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
