using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Extensions;

namespace TutoProxy.Server.Communication {
    public interface IUdpServer : IDisposable {
        Task Listen();
        Task SendResponse(UdpDataResponseModel response);
        void Disconnect(SocketAddressModel socketAddress, Int64 totalTransfered);
    }

    public class UdpServer : BaseServer, IUdpServer {
        readonly System.Net.Sockets.UdpClient udpServer;
        readonly CancellationTokenSource cts;
        readonly CancellationToken cancellationToken;
        readonly TimeSpan receiveTimeout;
        DateTime requestLogTimer = DateTime.Now;
        DateTime responseLogTimer = DateTime.Now;
        Int64 totalTransmitted;
        Int64 totalReceived;

        protected readonly ConcurrentDictionary<int, UdpClient> udpClients = new();

        public UdpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor, TimeSpan receiveTimeout)
            : base(port, localEndPoint, dataTransferService, logger, processMonitor) {
            udpServer = new System.Net.Sockets.UdpClient(new IPEndPoint(localEndPoint.Address, port));
            udpServer.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.ReuseAddress, true);
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
                            var client = AddRemoteEndPoint(result.RemoteEndPoint);
                            await dataTransferService.SendUdpRequest(new UdpDataRequestModel() {
                                Port = Port, OriginPort = result.RemoteEndPoint.Port,
                                Data = result.Buffer
                            });
                            totalReceived += result.Buffer.Length;
                            if(requestLogTimer <= DateTime.Now) {
                                requestLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                                logger.Information($"udp request from {result.RemoteEndPoint}, bytes:{result.Buffer.ToShortDescriptions()}");
                                processMonitor.UdpClientData(client, totalTransmitted, totalReceived);
                            }
                        }
                    } catch(System.Net.Sockets.SocketException ex) {
                        logger.Error($"udp: {ex.Message}");
                    }
                }
            }, cts.Token);
        }

        public async Task SendResponse(UdpDataResponseModel response) {
            if(cancellationToken.IsCancellationRequested) {
                await dataTransferService.DisconnectUdp(new SocketAddressModel() { Port = Port, OriginPort = response.OriginPort }, Int64.MinValue);
                logger.Error($"udp({Port}) response to canceled {response.OriginPort}");
                return;
            }
            if(!udpClients.TryGetValue(response.OriginPort, out UdpClient? client)) {
                await dataTransferService.DisconnectUdp(new SocketAddressModel() { Port = Port, OriginPort = response.OriginPort }, Int64.MinValue);
                logger.Error($"udp({Port}) response to missed {response.OriginPort}");
                return;
            }
            var transmitted = await udpServer.SendAsync(response.Data, client.EndPoint, cancellationToken);
            totalTransmitted += transmitted;
            if(responseLogTimer <= DateTime.Now) {
                responseLogTimer = DateTime.Now.AddSeconds(UdpSocketParams.LogUpdatePeriod);
                logger.Information($"udp response to {client.EndPoint}, bytes:{response.Data?.ToShortDescriptions()}");
                processMonitor.UdpClientData(client, totalTransmitted, totalReceived);
            }
        }

        public async void Disconnect(SocketAddressModel socketAddress, Int64 totalTransfered) {
            if(cancellationToken.IsCancellationRequested) {
                return;
            }
            if(!udpClients.TryRemove(socketAddress.OriginPort, out UdpClient? client)) {
                return;
            }

            await client.DisposeAsync();
        }

        public override async void Dispose() {
            cts.Cancel();
            udpServer.Close();

            foreach(var item in udpClients.Values.ToList()) {
                if(udpClients.TryGetValue(item.EndPoint.Port, out UdpClient? client)) {
                    await client.DisposeAsync();
                }
            }
            GC.SuppressFinalize(this);
        }

        protected UdpClient AddRemoteEndPoint(IPEndPoint endPoint) {
            return udpClients.AddOrUpdate(endPoint.Port,
                 (k) => {
                     Debug.WriteLine($"AddRemoteEndPoint: add {k}");
                     var newCLient = new UdpClient(this, dataTransferService, logger, processMonitor,
                                 endPoint, receiveTimeout, RemoveExpiredRemoteEndPoint);
                     return newCLient;
                 },
                 (k, v) => {
                     //Debug.WriteLine($"AddRemoteEndPoint: update {k}");
                     v.StartTimeoutTimer();
                     return v;
                 }
             );
        }

        async void RemoveExpiredRemoteEndPoint(int port) {
            Debug.WriteLine($"RemoveExpiredRemoteEndPoint: {port}");
            if(udpClients.TryRemove(port, out UdpClient? client)) {
                await client.DisposeAsync();
            }
        }
    }
}
