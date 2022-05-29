using System.Net;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    public class Client : IDisposable {
        public IClientProxy ClientProxy { get; private set; }
        public IEnumerable<int>? TcpPorts { get; private set; }
        public IEnumerable<int>? UdpPorts { get; private set; }

        readonly Dictionary<int, UdpConnection> udpConnections = new();

        public Client(IPEndPoint localEndPoint, IClientProxy clientProxy, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts, ILogger logger,
                    IServiceProvider serviceProvider) {
            ClientProxy = clientProxy;
            TcpPorts = tcpPorts;
            UdpPorts = udpPorts;

            if(udpPorts != null) {
                udpConnections = udpPorts
                    .ToDictionary(k => k, v => new UdpConnection(v, localEndPoint, serviceProvider.GetRequiredService<IDataTransferService>(), logger));
            } else {
                udpConnections = new();
            }
        }

        public void Listen() {
            if(udpConnections != null) {
                Task.WhenAll(udpConnections.Values.Select(x => x.Listen()));
            }
        }

        public async Task SendUdpResponse(UdpDataResponseModel response) {
            if(udpConnections.TryGetValue(response.Port, out UdpConnection? udpConnection)) {
                await udpConnection.SendResponse(response);
            }
        }

        public void Dispose() {
            foreach(var item in udpConnections.Values) {
                item.Dispose();
            }
        }
    }
}
