using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.SignalR;
using TutoProxy.Server.Services;

namespace TutoProxy.Server.Communication {
    public class Client : IDisposable {
        public IClientProxy ClientProxy { get; private set; }
        public List<int>? TcpPorts { get; private set; }
        public List<int>? UdpPorts { get; private set; }

        readonly List<UdpListener>? udpListeners = null;

        public Client(IPEndPoint localEndPoint, IClientProxy clientProxy, List<int>? tcpPorts, List<int>? udpPorts, ILogger logger,
                    IRequestProcessingService requestProcessingService) {
            ClientProxy = clientProxy;
            TcpPorts = tcpPorts;
            UdpPorts = udpPorts;

            if(udpPorts != null) {
                udpListeners = udpPorts
                    .Select(x => new UdpListener(x, localEndPoint, requestProcessingService, logger))
                    .ToList();
            }
        }

        public async Task Listen() {
            if(udpListeners != null) {
                await Task.WhenAll(udpListeners.Select(x => x.Listen()));
            }
        }

        public void Dispose() {
            if(udpListeners != null) {
                foreach(var item in udpListeners) {
                    item.Dispose();
                }
            }
        }
    }
}
