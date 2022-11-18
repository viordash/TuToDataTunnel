using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Communication {
    public class HubClient : IDisposable {
        public IClientProxy ClientProxy { get; private set; }
        public IEnumerable<int>? TcpPorts { get; private set; }
        public IEnumerable<int>? UdpPorts { get; private set; }

        readonly Dictionary<int, TcpServer> tcpServers = new();
        readonly Dictionary<int, UdpServer> udpServers = new();
        readonly CancellationTokenSource cts;
        readonly ILogger logger;
        readonly BufferBlock<TcpStreamDataModel> outgoingQueue;

        public HubClient(IPEndPoint localEndPoint, IClientProxy clientProxy, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts,
                    IServiceProvider serviceProvider) {
            ClientProxy = clientProxy;
            TcpPorts = tcpPorts;
            UdpPorts = udpPorts;

            cts = new CancellationTokenSource();
            outgoingQueue = new BufferBlock<TcpStreamDataModel>();

            var dataTransferService = serviceProvider.GetRequiredService<IDataTransferService>();
            logger = serviceProvider.GetRequiredService<ILogger>();
            if(tcpPorts != null) {
                tcpServers = tcpPorts
                    .ToDictionary(k => k, v => new TcpServer(v, localEndPoint, dataTransferService, this, logger));
            } else {
                tcpServers = new();
            }

            if(udpPorts != null) {
                udpServers = udpPorts
                    .ToDictionary(k => k, v => new UdpServer(v, localEndPoint, dataTransferService, this, logger, UdpSocketParams.ReceiveTimeout));
            } else {
                udpServers = new();
            }
        }

        public void Dispose() {
            cts.Cancel();
            cts.Dispose();

            foreach(var item in tcpServers.Values) {
                item.Dispose();
            }
            foreach(var item in udpServers.Values) {
                item.Dispose();
            }
        }

        public void Listen() {
            if(tcpServers != null) {
                Task.WhenAll(tcpServers.Values.Select(x => x.Listen()));
            }
            if(udpServers != null) {
                Task.WhenAll(udpServers.Values.Select(x => x.Listen()));
            }
        }


        public async Task SendUdpResponse(UdpDataResponseModel response) {
            if(!udpServers.TryGetValue(response.Port, out UdpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Udp, response.Port);
            }
            await server.SendResponse(response);
        }

        public void DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            if(!udpServers.TryGetValue(socketAddress.Port, out UdpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Udp, socketAddress.Port);
            }
            server.Disconnect(socketAddress, totalTransfered);
        }

        public void DisconnectTcp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            if(!tcpServers.TryGetValue(socketAddress.Port, out TcpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Tcp, socketAddress.Port);
            }
            server.Disconnect(socketAddress, totalTransfered);
        }

        public async Task StreamFromTcpClient(IAsyncEnumerable<TcpStreamDataModel> streamData) {
            await foreach(var data in streamData) {
                try {
                    if(tcpServers.TryGetValue(data.Port, out TcpServer? server)) {
                        await server.SendData(data);
                    } else {
                        logger.Error($"tcp server {data.Port} not found");
                    }
                } catch(Exception ex) {
                    logger.Error(ex.GetBaseException().Message);
                }
            }
            Debug.WriteLine($"                  ------ server stopped");
        }

        public async Task PushOutgoingTcpData(TcpStreamDataModel streamData) {
            while(outgoingQueue.Count > 20 && !cts.IsCancellationRequested) {
                await Task.Delay(10);
            }
            await outgoingQueue.SendAsync(streamData);
        }

        public IAsyncEnumerable<TcpStreamDataModel> OutgoingStream() {
            return outgoingQueue.ReceiveAllAsync(cts.Token);
        }
    }
}
