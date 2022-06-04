using System.Net;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    public class HubClient : IDisposable {
        public IClientProxy ClientProxy { get; private set; }
        public IEnumerable<int>? TcpPorts { get; private set; }
        public IEnumerable<int>? UdpPorts { get; private set; }

        readonly Dictionary<int, UdpServer> udpServers = new();

        public HubClient(IPEndPoint localEndPoint, IClientProxy clientProxy, IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts,
                    IServiceProvider serviceProvider) {
            ClientProxy = clientProxy;
            TcpPorts = tcpPorts;
            UdpPorts = udpPorts;

            var dataTransferService = serviceProvider.GetRequiredService<IDataTransferService>();
            var logger = serviceProvider.GetRequiredService<ILogger>();
            if(udpPorts != null) {
                udpServers = udpPorts
                    .ToDictionary(k => k, v => new UdpServer(v, localEndPoint, dataTransferService, logger));
            } else {
                udpServers = new();
            }
        }

        public void Listen() {
            if(udpServers != null) {
                Task.WhenAll(udpServers.Values.Select(x => x.Listen()));
            }
        }

        public async Task SendUdpResponse(UdpDataResponseModel response) {
            if(udpServers.TryGetValue(response.Port, out UdpServer? udpConnection)) {
                await udpConnection.SendResponse(response);
            }
        }

        public void Dispose() {
            foreach(var item in udpServers.Values) {
                item.Dispose();
            }
        }
    }
}
