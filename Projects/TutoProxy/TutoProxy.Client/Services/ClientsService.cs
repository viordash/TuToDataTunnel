using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using TutoProxy.Client.Communication;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Services {
    public interface IClientsService {
        void Start(IPAddress localIpAddress, List<int>? tcpPorts, List<int>? udpPorts);
        TcpClient ObtainTcpClient(int port, int originPort);
        void RemoveTcpClient(int port, int originPort);
        UdpClient ObtainUdpClient(int port, int originPort);
        void RemoveUdpClient(int port, int originPort);
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

        public TcpClient ObtainTcpClient(int port, int originPort) {
            var commonPortClients = tcpClients.GetOrAdd(port,
                    _ => {
                        if(tcpPorts == null || !tcpPorts.Contains(port)) {
                            throw new ClientNotFoundException(DataProtocol.Tcp, port);
                        }
                        Debug.WriteLine($"ObtainClient: add tcp for port {port}");
                        return new ConcurrentDictionary<int, TcpClient>();
                    }
                );

            var client = commonPortClients.GetOrAdd(originPort,
                _ => {
                    Debug.WriteLine($"ObtainClient: add tcp for OriginPort {originPort}");
                    return clientFactory.CreateTcp(localIpAddress, port, originPort, TimeoutTcpClient);
                }
            );
            client.Refresh();
            return client;
        }

        public void RemoveTcpClient(int port, int originPort) {
            if(tcpClients.TryGetValue(port, out ConcurrentDictionary<int, TcpClient>? removingClients)
                && removingClients.TryRemove(originPort, out TcpClient? removedClient)) {
                removedClient.Dispose();
                Debug.WriteLine($"RemoveTcpClient: {port}, {originPort}");
            }
        }

        void TimeoutTcpClient(int port, int originPort) {
            RemoveTcpClient(port, originPort);
        }

        public UdpClient ObtainUdpClient(int port, int originPort) {
            var commonPortClients = udpClients.GetOrAdd(port,
                    _ => {
                        if(udpPorts == null || !udpPorts.Contains(port)) {
                            throw new ClientNotFoundException(DataProtocol.Udp, port);
                        }
                        Debug.WriteLine($"ObtainClient: add udp for port {port}");
                        return new ConcurrentDictionary<int, UdpClient>();
                    }
                );

            var client = commonPortClients.GetOrAdd(originPort,
                _ => {
                    Debug.WriteLine($"ObtainClient: add udp for OriginPort {originPort}");
                    return clientFactory.CreateUdp(localIpAddress, port, originPort, TimeoutUdpClient);
                }
            );
            client.Refresh();
            return client;
        }

        public void RemoveUdpClient(int port, int originPort) {
            if(udpClients.TryGetValue(port, out ConcurrentDictionary<int, UdpClient>? removingClients)) {
                if(removingClients.TryRemove(originPort, out UdpClient? removedClient)) {
                    Debug.WriteLine($"RemoveUdpClient: {port}, {originPort}");
                    removedClient.Dispose();
                }
            }
        }

        void TimeoutUdpClient(int port, int originPort) {
            RemoveUdpClient(port, originPort);
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
