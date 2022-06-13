using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using TutoProxy.Client.Communication;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Models;

namespace TutoProxy.Client.Services {
    public interface IClientsService {
        void Start(IPAddress localIpAddress, List<int>? tcpPorts, List<int>? udpPorts);
        TcpClient ObtainClient(TcpDataRequestModel request);
        UdpClient ObtainClient(UdpDataRequestModel request);
        void Stop();
    }

    public class ClientsService : IClientsService {
        readonly ILogger logger;
        readonly IClientFactory clientFactory;
        IPAddress localIpAddress;
        List<int>? tcpPorts;
        List<int>? udpPorts;

        protected readonly ConcurrentDictionary<int, ConcurrentDictionary<int, TcpClient>> tcpClients = new();
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

        public TcpClient ObtainClient(TcpDataRequestModel request) {
            var commonPortClients = tcpClients.GetOrAdd(request.Port,
                    _ => {
                        if(tcpPorts == null || !tcpPorts.Contains(request.Port)) {
                            throw new ClientNotFoundException(DataProtocol.Tcp, request.Port);
                        }
                        Debug.WriteLine($"ObtainClient: add tcp for port {request.Port}");
                        return new ConcurrentDictionary<int, TcpClient>();
                    }
                );

            var client = commonPortClients.GetOrAdd(request.OriginPort,
                _ => {
                    Debug.WriteLine($"ObtainClient: add tcp for OriginPort {request.OriginPort}");
                    return clientFactory.Create(localIpAddress, request, TimeoutTcpClient);
                }
            );
            client.Refresh();
            return client;
        }

        void TimeoutTcpClient(int port, int originPort) {
            if(tcpClients.TryGetValue(port, out ConcurrentDictionary<int, TcpClient>? removingClients)) {
                if(removingClients.TryRemove(originPort, out TcpClient? removedClient)) {
                    Debug.WriteLine($"TimeoutTcpClient: remove: {port}, {originPort}");
                    removedClient.Dispose();
                }
            }
        }

        public UdpClient ObtainClient(UdpDataRequestModel request) {
            var commonPortClients = udpClients.GetOrAdd(request.Port,
                    _ => {
                        if(udpPorts == null || !udpPorts.Contains(request.Port)) {
                            throw new ClientNotFoundException(DataProtocol.Udp, request.Port);
                        }
                        Debug.WriteLine($"ObtainClient: add udp for port {request.Port}");
                        return new ConcurrentDictionary<int, UdpClient>();
                    }
                );

            var client = commonPortClients.GetOrAdd(request.OriginPort,
                _ => {
                    Debug.WriteLine($"ObtainClient: add udp for OriginPort {request.OriginPort}");
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
            foreach(var client in tcpClients.Values.SelectMany(x => x.Values)) {
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
