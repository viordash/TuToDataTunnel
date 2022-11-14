using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.Communication {
    public class UdpServer : BaseServer {
        #region inner classes
        public class RemoteEndPoint : IDisposable {
            readonly Action<int> timeoutAction;
            public IPEndPoint EndPoint { get; private set; }
            readonly System.Timers.Timer timeoutTimer;

            public RemoteEndPoint(IPEndPoint endPoint, TimeSpan receiveTimeout, Action<int> timeoutAction) {
                EndPoint = endPoint;
                this.timeoutAction = timeoutAction;

                timeoutTimer = new(receiveTimeout.TotalMilliseconds);
                timeoutTimer.Elapsed += OnTimedEvent;
                timeoutTimer.AutoReset = false;

                StartTimeoutTimer();
            }

            void OnTimedEvent(object? source, ElapsedEventArgs e) {
                timeoutAction(EndPoint.Port);
            }

            public void StartTimeoutTimer() {
                timeoutTimer.Enabled = false;
                timeoutTimer.Enabled = true;
            }

            public void Dispose() {
                timeoutTimer.Enabled = false;
                timeoutTimer.Elapsed -= OnTimedEvent;
            }
        }
        #endregion

        readonly UdpClient udpServer;
        readonly CancellationTokenSource cts;
        readonly CancellationToken cancellationToken;
        readonly TimeSpan receiveTimeout;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        protected readonly ConcurrentDictionary<int, RemoteEndPoint> remoteEndPoints = new();

        public UdpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, HubClient hubClient, ILogger logger, TimeSpan receiveTimeout)
            : base(port, localEndPoint, dataTransferService, hubClient, logger) {
            udpServer = new UdpClient(new IPEndPoint(localEndPoint.Address, port));
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            cts = new CancellationTokenSource();
            cancellationToken = cts.Token;
            this.receiveTimeout = receiveTimeout;
        }

        public Task Listen() {
            return Task.Run(async () => {
                while(!cancellationToken.IsCancellationRequested) {
                    try {
                        while(!cancellationToken.IsCancellationRequested) {
                            var result = await udpServer.ReceiveAsync(cancellationToken);
                            AddRemoteEndPoint(result.RemoteEndPoint);
                            await dataTransferService.SendUdpRequest(new UdpDataRequestModel(port, result.RemoteEndPoint.Port, result.Buffer));
                            if(requestLogTimer <= DateTime.Now) {
                                requestLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                                logger.Information($"udp request from {result.RemoteEndPoint}, bytes:{result.Buffer.ToShortDescriptions()}");
                            }
                        }
                    } catch(SocketException ex) {
                        logger.Error($"udp: {ex.Message}");
                    }
                }
            }, cancellationToken);
        }

        public async Task SendResponse(UdpDataResponseModel response) {
            if(cancellationToken.IsCancellationRequested) {
                await dataTransferService.DisconnectUdp(new SocketAddressModel(port, response.OriginPort), Int64.MinValue);
                logger.Error($"udp({port}) response to canceled {response.OriginPort}");
                return;
            }
            if(!remoteEndPoints.TryGetValue(response.OriginPort, out RemoteEndPoint? remoteEndPoint)) {
                await dataTransferService.DisconnectUdp(new SocketAddressModel(port, response.OriginPort), Int64.MinValue);
                logger.Error($"udp({port}) response to missed {response.OriginPort}");
                return;
            }
            await udpServer.SendAsync(response.Data, remoteEndPoint.EndPoint, cancellationToken);
            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                logger.Information($"udp response to {remoteEndPoint.EndPoint}, bytes:{response.Data?.ToShortDescriptions()}");
            }
        }

        public void Disconnect(SocketAddressModel socketAddress, Int64 totalTransfered) {
            if(cancellationToken.IsCancellationRequested) {
                return;
            }
            if(!remoteEndPoints.TryRemove(socketAddress.OriginPort, out RemoteEndPoint? remoteEndPoint)) {
                return;
            }

            remoteEndPoint.Dispose();
        }

        public override void Dispose() {
            cts.Cancel();
            cts.Dispose();
            udpServer.Dispose();
            foreach(var item in remoteEndPoints.Values) {
                item.Dispose();
            }
        }

        protected void AddRemoteEndPoint(IPEndPoint endPoint) {
            remoteEndPoints.AddOrUpdate(endPoint.Port,
                (k) => {
                    Debug.WriteLine($"AddRemoteEndPoint: add {k}");
                    return new RemoteEndPoint(endPoint, receiveTimeout, RemoveExpiredRemoteEndPoint);
                },
                (k, v) => {
                    //Debug.WriteLine($"AddRemoteEndPoint: update {k}");
                    v.StartTimeoutTimer();
                    return v;
                }
            );
        }

        void RemoveExpiredRemoteEndPoint(int port) {
            Debug.WriteLine($"RemoveExpiredRemoteEndPoint: {port}");
            remoteEndPoints.TryRemove(port, out _);
        }
    }
}
