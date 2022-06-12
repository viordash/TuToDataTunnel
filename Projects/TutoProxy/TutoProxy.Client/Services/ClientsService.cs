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

    internal class ClientsService : IClientsService {
        readonly ILogger logger;
        IPAddress localIpAddress;
        List<int>? tcpPorts;
        List<int>? udpPorts;
        readonly List<TcpClient> tcpClients = new();

        readonly ConcurrentDictionary<int, ConcurrentDictionary<int, UdpClient>> udpClients = new();

        public ClientsService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;

            localIpAddress = IPAddress.None;
        }

        public void Start(IPAddress localIpAddress, List<int>? tcpPorts, List<int>? udpPorts) {
            Stop();
            this.localIpAddress = localIpAddress;
            this.tcpPorts = tcpPorts;
            this.udpPorts = udpPorts;
        }

        public TcpClient GetTcpClient(TcpDataRequestModel request) {
            var connection = tcpClients.FirstOrDefault(x => x.Port == request.Port);
            if(connection == null) {
                throw new ClientNotFoundException(DataProtocol.Tcp, request.Port);
            }
            return connection;
        }

        public UdpClient ObtainUdpClient(UdpDataRequestModel request) {
            var onePortClients = udpClients.GetOrAdd(request.Port,
                    _ => {
                        if(udpPorts == null || !udpPorts.Contains(request.Port)) {
                            throw new ClientNotFoundException(DataProtocol.Udp, request.Port);
                        }
                        Debug.WriteLine($"GetUdpClient: add by port {request.Port}");
                        return new ConcurrentDictionary<int, UdpClient>();
                    }
                );

            var connection = onePortClients.GetOrAdd(request.OriginPort,
                _ => {
                    Debug.WriteLine($"GetUdpClient: add by OriginPort {request.OriginPort}");
                    return new UdpClient(new IPEndPoint(localIpAddress, request.Port), request.OriginPort, logger);
                }
            );
            return connection;
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
