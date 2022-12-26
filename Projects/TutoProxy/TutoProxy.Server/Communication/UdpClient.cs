using System.Net;
using System.Timers;
using TutoProxy.Server.Services;

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
    }
}
