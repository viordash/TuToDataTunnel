using System.Net;
using System.Timers;
using Terminal.Gui;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.Communication {

    public class UdpClient : BaseClient {
        readonly Action<int> timeoutAction;
        readonly System.Timers.Timer timeoutTimer;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;

        Int64 totalTransmitted;
        Int64 totalReceived;

        public IPEndPoint EndPoint { get; private set; }

        public UdpClient(BaseServer udpServer, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor,
           IPEndPoint endPoint, TimeSpan receiveTimeout, Action<int> timeoutAction)
            : base(udpServer, endPoint.Port, dataTransferService, logger, processMonitor) {

            EndPoint = endPoint;
            this.timeoutAction = timeoutAction;

            timeoutTimer = new(receiveTimeout.TotalMilliseconds);
            timeoutTimer.Elapsed += OnTimedEvent;
            timeoutTimer.AutoReset = false;

            StartTimeoutTimer();
            processMonitor.ConnectUdpClient(this);
            logger.Information($"{this}, created");
        }

        public override string ToString() {
            return $"udp({base.ToString()})";
        }

        public override async ValueTask DisposeAsync() {
            await base.DisposeAsync();

            timeoutTimer.Enabled = false;
            timeoutTimer.Elapsed -= OnTimedEvent;

            processMonitor.DisconnectUdpClient(this);
            logger.Information($"{this}, disconnected, tx:{totalTransmitted}, rx:{totalReceived}");
        }


        void OnTimedEvent(object? source, ElapsedEventArgs e) {
            timeoutAction(Port);
        }

        public void StartTimeoutTimer() {
            timeoutTimer.Enabled = false;
            timeoutTimer.Enabled = true;
        }

        public async Task SendRequestAsync(byte[] payload, CancellationToken cancellationToken) {
            await dataTransferService.SendUdpRequest(new UdpDataRequestModel() {
                Port = Port, OriginPort = OriginPort,
                Data = payload
            }, cancellationToken);
            totalReceived += payload.Length;
            if(requestLogTimer <= DateTime.Now) {
                requestLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                logger.Information($"{this} request, bytes:{payload.ToArray().ToShortDescriptions()}");
                processMonitor.UdpClientData(this, totalTransmitted, totalReceived);
            }
        }

        public async Task SendResponseAsync(System.Net.Sockets.UdpClient socket, byte[] response, CancellationToken cancellationToken) {
            var transmitted = await socket.SendAsync(response, EndPoint, cancellationToken);
            totalTransmitted += transmitted;
            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                logger.Information($"{this} response, bytes:{response.ToShortDescriptions()}");
                processMonitor.UdpClientData(this, totalTransmitted, totalReceived);
            }
        }
    }
}
