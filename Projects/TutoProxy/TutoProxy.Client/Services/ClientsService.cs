using System.Net;
using TutoProxy.Client.Communication;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Models;

namespace TutoProxy.Client.Services {
    public interface IClientsService {
        void Start(IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts);
        TcpClient GetTcpClient(int port);
        UdpClient GetUdpClient(int port);
        void Stop();
    }

    internal class ClientsService : IClientsService {
        readonly ILogger logger;
        readonly List<TcpClient> tcpClients = new();
        readonly List<UdpClient> udpClients = new();

        public ClientsService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public void Start(IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts) {
            Stop();

            var ipLocalAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0];

            if(tcpPorts != null) {
                tcpClients.AddRange(tcpPorts.Select(x => new TcpClient(new IPEndPoint(ipLocalAddress, x), logger)));
            }
            if(udpPorts != null) {
                udpClients.AddRange(udpPorts.Select(x => new UdpClient(new IPEndPoint(ipLocalAddress, x), logger)));
            }
        }

        public TcpClient GetTcpClient(int port) {
            var connection = tcpClients.FirstOrDefault(x => x.Port == port);
            if(connection == null) {
                throw new ClientNotFoundException(DataProtocol.Tcp, port);
            }
            return connection;
        }

        public UdpClient GetUdpClient(int port) {
            var connection = udpClients.FirstOrDefault(x => x.Port == port);
            if(connection == null) {
                throw new ClientNotFoundException(DataProtocol.Udp, port);
            }
            return connection;
        }

        public void Stop() {
            foreach(var client in tcpClients) {
                client.Dispose();
            }
            tcpClients.Clear();

            foreach(var client in udpClients) {
                client.Dispose();
            }
            udpClients.Clear();
        }
    }
}
