using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using TutoProxy.Client.Communication;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Models;

namespace TutoProxy.Client.Services {
    public interface IClientsService {
        void Start(IPAddress localIpAddress, List<int>? tcpPorts, List<int>? udpPorts);
        TcpClient GetTcpClient(TcpDataRequestModel request);
        UdpClient ObtainUdpClient(UdpDataRequestModel request);
        void Stop();
    }

    public class ClientsService : IClientsService {
        readonly ILogger logger;
        readonly IClientFactory clientFactory;
        IPAddress localIpAddress;
        List<int>? tcpPorts;
        List<int>? udpPorts;
        readonly List<TcpClient> tcpClients = new();

        protected readonly ConcurrentDictionary<int, ConcurrentDictionary<int, UdpClient>> udpClients = new();

        public ClientsService(
            ILogger logger,
            IClientFactory clientFactory
            ) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(clientFactory, nameof(clientFactory));
            this.logger = logger;
            this.clientFactory = clientFactory;

            localIpAddress = IPAddress.None;
        }

        public void Start(IPAddress localIpAddress, List<int>? tcpPorts, List<int>? udpPorts) {
            Stop();
            this.localIpAddress = localIpAddress;
            this.tcpPorts = tcpPorts;
            this.udpPorts = udpPorts;
        }

        public TcpClient GetTcpClient(TcpDataRequestModel request) {
            var client = tcpClients.FirstOrDefault(x => x.Port == request.Port);
            if(client == null) {
                throw new ClientNotFoundException(DataProtocol.Tcp, request.Port);
            }
            client.Refresh();
            return client;
        }

        public UdpClient ObtainUdpClient(UdpDataRequestModel request) {
            var commonPortClients = udpClients.GetOrAdd(request.Port,
                    _ => {
                        if(udpPorts == null || !udpPorts.Contains(request.Port)) {
                            throw new ClientNotFoundException(DataProtocol.Udp, request.Port);
                        }
                        Debug.WriteLine($"ObtainUdpClient: add for port {request.Port}");
                        return new ConcurrentDictionary<int, UdpClient>();
                    }
                );

            var client = commonPortClients.GetOrAdd(request.OriginPort,
                _ => {
                    Debug.WriteLine($"ObtainUdpClient: add for OriginPort {request.OriginPort}");
                    return clientFactory.Create(localIpAddress, request, TimeoutUdpClient);
                }
            );
            client.Refresh();
            return client;
        }

        void TimeoutUdpClient(int port, int originPort) {
            if(udpClients.TryGetValue(port, out ConcurrentDictionary<int, UdpClient>? removingClients)) {
                if(removingClients.TryRemove(originPort, out UdpClient? removedClient)) {
                    Debug.WriteLine($"TimeoutUdpClient: remove: {port}, {originPort}");
                    removedClient.Dispose();
                }
            }
        }

        public void Stop() {
            foreach(var client in tcpClients) {
                client.Dispose();
            }
            tcpClients.Clear();

            foreach(var client in udpClients.Values.SelectMany(x => x.Values)) {
                client.Dispose();
            }
            udpClients.Clear();
        }
    }
}
