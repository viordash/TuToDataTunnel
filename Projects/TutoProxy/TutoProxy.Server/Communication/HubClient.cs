using System.Collections.Frozen;
using System.Net;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Communication {
    public class HubClient : IAsyncDisposable {
        public IEnumerable<int>? TcpPorts { get; private set; }
        public IEnumerable<int>? UdpPorts { get; private set; }

        readonly FrozenDictionary<int, ITcpServer> tcpServers;
        readonly FrozenDictionary<int, IUdpServer> udpServers;
        readonly CancellationTokenSource cts;

        public HubClient(IPEndPoint localEndPoint, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts,
                    IServiceProvider serviceProvider) {
            TcpPorts = tcpPorts;
            UdpPorts = udpPorts;

            cts = new CancellationTokenSource();

            var serverFactory = serviceProvider.GetRequiredService<IServerFactory>();
            if(tcpPorts != null) {
                tcpServers = tcpPorts
                    .ToFrozenDictionary(k => k, v => serverFactory.CreateTcp(v, localEndPoint));
            } else {
                tcpServers = FrozenDictionary<int, ITcpServer>.Empty;
            }

            if(udpPorts != null) {
                udpServers = udpPorts
                    .ToFrozenDictionary(k => k, v => serverFactory.CreateUdp(v, localEndPoint, UdpSocketParams.ReceiveTimeout));
            } else {
                udpServers = FrozenDictionary<int, IUdpServer>.Empty;
            }
        }

        public async ValueTask DisposeAsync() {
            cts.Cancel();
            cts.Dispose();

            foreach(var item in tcpServers.Values) {
                await item.DisposeAsync();
            }
            foreach(var item in udpServers.Values) {
                await item.DisposeAsync();
            }
        }

        public Task Listen() {
            var tasks = tcpServers.Values.Select(x => x.Listen())
                .Concat(udpServers.Values.Select(x => x.Listen()));
            return Task.WhenAll(tasks);
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

        public async Task DisconnectUdpAsync(SocketAddressModel socketAddress, Int64 totalTransfered) {
            if(!udpServers.TryGetValue(socketAddress.Port, out IUdpServer? server)) {
                throw new SocketPortNotBoundException(DataProtocol.Udp, socketAddress.Port);
            }
            await server.DisconnectAsync(socketAddress, totalTransfered);
        }
    }
}
