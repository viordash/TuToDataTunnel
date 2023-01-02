using System.Net;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Communication {
    public class HubClient : IDisposable {
        public IClientProxy ClientProxy { get; private set; }
        public IEnumerable<int>? TcpPorts { get; private set; }
        public IEnumerable<int>? UdpPorts { get; private set; }

        readonly Dictionary<int, ITcpServer> tcpServers = new();
        readonly Dictionary<int, IUdpServer> udpServers = new();
        readonly CancellationTokenSource cts;

        public HubClient(IPEndPoint localEndPoint, IClientProxy clientProxy, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts,
                    IServiceProvider serviceProvider) {
            ClientProxy = clientProxy;
            TcpPorts = tcpPorts;
            UdpPorts = udpPorts;

            cts = new CancellationTokenSource();

            var serverFactory = serviceProvider.GetRequiredService<IServerFactory>();
            if(tcpPorts != null) {
                tcpServers = tcpPorts
                    .ToDictionary(k => k, v => serverFactory.CreateTcp(v, localEndPoint));
            } else {
                tcpServers = new();
            }

            if(udpPorts != null) {
                udpServers = udpPorts
                    .ToDictionary(k => k, v => serverFactory.CreateUdp(v, localEndPoint, UdpSocketParams.ReceiveTimeout));
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

        public ValueTask<int> SendTcpResponse(TcpDataResponseModel response) {
            if(!tcpServers.TryGetValue(response.Port, out ITcpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Tcp, response.Port);
            }
            return server.SendResponse(response, cts.Token);
        }

        public ValueTask<bool> DisconnectTcp(SocketAddressModel socketAddress) {
            if(!tcpServers.TryGetValue(socketAddress.Port, out ITcpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Tcp, socketAddress.Port);
            }
            return server.DisconnectAsync(socketAddress);
        }

        public async Task SendUdpResponse(UdpDataResponseModel response) {
            if(!udpServers.TryGetValue(response.Port, out IUdpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Udp, response.Port);
            }
            await server.SendResponse(response);
        }

        public void DisconnectUdp(SocketAddressModel socketAddress, Int64 totalTransfered) {
            if(!udpServers.TryGetValue(socketAddress.Port, out IUdpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Udp, socketAddress.Port);
            }
            server.Disconnect(socketAddress, totalTransfered);
        }
    }
}
