using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TutoProxy.Client.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Extensions;
using TuToProxy.Core.Queue;

namespace TutoProxy.Client.Communication {
    public class TcpClient : BaseClient<Socket> {
        const int firstFrame = 0;
        int? localPort = null;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        readonly Timer forceCloseTimer;
        readonly SortedQueue incomingQueue;

        bool shutdownReceive;
        bool shutdownTransmit;
        Int64 frame;

        public int TotalTransmitted { get; set; }
        public int TotalReceived { get; set; }

        protected override TimeSpan ReceiveTimeout { get { return TcpSocketParams.ReceiveTimeout; } }

        public TcpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
            : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {
            forceCloseTimer = new(OnForceCloseTimedEvent);
            frame = firstFrame - 1;
            incomingQueue = new SortedQueue(firstFrame, TcpSocketParams.QueueMaxSize);
        }

        void TryShutdown(SocketShutdown how) {
            lock(this) {
                switch(how) {
                    case SocketShutdown.Receive:
                        shutdownReceive = true;
                        break;
                    case SocketShutdown.Send:
                        shutdownTransmit = true;
                        if(socket.Connected) {
                            try {
                                socket.Shutdown(SocketShutdown.Send);
                            } catch(Exception) { }
                        }
                        break;
                    case SocketShutdown.Both:
                        shutdownReceive = true;
                        shutdownTransmit = true;
                        break;
                }


                if(shutdownReceive && shutdownTransmit) {
                    if(socket.Connected) {
                        try {
                            socket.Shutdown(SocketShutdown.Both);
                        } catch(SocketException) { }
                    }
                    clientsService.RemoveTcpClient(Port, OriginPort);
                    forceCloseTimer.Dispose();
                } else {
                    StartClosingTimer();
                }
            }
        }

        void OnForceCloseTimedEvent(object? state) {
            Debug.WriteLine($"tcp({localPort}) , o-port: {OriginPort}, attempt to close");
            TryShutdown(SocketShutdown.Both);
        }

        public void StartClosingTimer() {
            forceCloseTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public void StopClosingTimer() {
            forceCloseTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public Int64 NextFrame() {
            return Interlocked.Increment(ref frame);
        }
        public Int64 LastFrame() {
            return Interlocked.Read(ref frame);
        }

        protected override void OnTimedEvent(object? state) { }

        protected override Socket CreateSocket() {
            var tcpClient = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, created");
            return tcpClient;
        }

        public override void Dispose() {
            socket.Close();
            base.Dispose();
            logger.Information($"tcp({localPort}) server: {serverEndPoint}, o-port: {OriginPort}, destroyed, tx:{TotalTransmitted}, rx:{TotalReceived}");
        }

        async void ReceivingStream(CancellationToken cancellationToken) {
            Memory<byte> receiveBuffer = new byte[TcpSocketParams.ReceiveBufferSize];

            bool remoteClosed = true;
            while(socket.Connected && !cancellationToken.IsCancellationRequested) {
                int receivedBytes;
                try {
                    receivedBytes = await socket.ReceiveAsync(receiveBuffer, SocketFlags.None, cancellationToken);
                    if(receivedBytes == 0) {
                        break;
                    }
                } catch(OperationCanceledException ex) {
                    logger.Error(ex.GetBaseException().Message);
                    break;
                } catch(SocketException ex) {
                    logger.Error(ex.GetBaseException().Message);
                    break;
                } catch(Exception ex) {
                    logger.Error(ex.GetBaseException().Message);
                    break;
                }
                TotalReceived += receivedBytes;
                var data = receiveBuffer[..receivedBytes].ToArray();
                remoteClosed = false;

                dataTunnelClient.PushOutgoingTcpData(new TcpStreamDataModel(Port, OriginPort, NextFrame(), data), cancellationToken);

                if(responseLogTimer <= DateTime.Now) {
                    responseLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) response from {serverEndPoint}, bytes:{data.ToShortDescriptions()}.");
                }
            }
            if(!remoteClosed) {
                dataTunnelClient.PushOutgoingTcpData(new TcpStreamDataModel(Port, OriginPort, LastFrame(), null), cancellationToken);
            }
            TryShutdown(SocketShutdown.Receive);
        }

        public async Task SendData(TcpStreamDataModel streamData, CancellationToken cancellationToken) {

            if(streamData.Data == null) {
                StartClosingTimer();
                if(incomingQueue.FrameWasTakenLast(streamData.Frame)) {
                    TryShutdown(SocketShutdown.Send);
                } else {
                    logger.Warning($"tcp({localPort}) request to {serverEndPoint}, not all data yet");
                }
                return;
            }

            incomingQueue.Enqueue(streamData.Frame, streamData.Data);

            if(!socket.Connected) {
                if(streamData.Frame == firstFrame) {
                    try {
                        await socket.ConnectAsync(serverEndPoint, cancellationToken);
                    } catch(Exception ex) {
                        logger.Error(ex.GetBaseException().Message);
                    }
                    localPort = (socket.LocalEndPoint as IPEndPoint)!.Port;
                    _ = Task.Run(() => ReceivingStream(cancellationToken));
                } else {
                    throw new TuToException($"tcp({localPort}) request to {serverEndPoint}, abnormally disconnected");
                }
            }

            if(incomingQueue.TryDequeue(out byte[]? orderedData)) {
                try {
                    TotalTransmitted += await socket.SendAsync(orderedData, SocketFlags.None, cancellationToken);
                } catch(SocketException) {
                } catch(ObjectDisposedException) {
                } catch(Exception ex) {
                    logger.Error(ex.GetBaseException().Message);
                }

                if(requestLogTimer <= DateTime.Now) {
                    requestLogTimer = DateTime.Now.AddSeconds(TcpSocketParams.LogUpdatePeriod);
                    logger.Information($"tcp({localPort}) request to {serverEndPoint}, bytes:{orderedData.ToShortDescriptions()}");
                }
            }

        }
    }
}
