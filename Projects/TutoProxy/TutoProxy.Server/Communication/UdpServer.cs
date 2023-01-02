using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    public interface IUdpServer : IDisposable {
        Task Listen();
        Task SendResponse(UdpDataResponseModel response);
        void Disconnect(SocketAddressModel socketAddress, Int64 totalTransfered);
    }

    public class UdpServer : BaseServer, IUdpServer {
        readonly System.Net.Sockets.UdpClient socket;
        readonly CancellationTokenSource cts;
        readonly CancellationToken cancellationToken;
        readonly TimeSpan receiveTimeout;

        protected readonly ConcurrentDictionary<int, UdpClient> udpClients = new();

        public UdpServer(int port, IPEndPoint localEndPoint, IDataTransferService dataTransferService, ILogger logger, IProcessMonitor processMonitor, TimeSpan receiveTimeout)
            : base(port, localEndPoint, dataTransferService, logger, processMonitor) {
            socket = new System.Net.Sockets.UdpClient(new IPEndPoint(localEndPoint.Address, port));
            socket.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.ReuseAddress, true);
            cts = new CancellationTokenSource();
            cancellationToken = cts.Token;
            this.receiveTimeout = receiveTimeout;
        }

        public Task Listen() {
            return Task.Run(async () => {
                while(!cancellationToken.IsCancellationRequested) {
                    try {
                        while(!cancellationToken.IsCancellationRequested) {
                            var result = await socket.ReceiveAsync(cancellationToken);
                            var client = AddRemoteEndPoint(result.RemoteEndPoint);
                            await client.SendRequestAsync(result.Buffer, cancellationToken);
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
            await client.SendResponseAsync(socket, response.Data, cancellationToken);
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
            cts.Dispose();
            socket.Close();

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
