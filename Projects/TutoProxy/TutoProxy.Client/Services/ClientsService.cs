using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TutoProxy.Client.Communication;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Models;

namespace TutoProxy.Client.Services {
    public interface IClientsService {
        void Start(IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts);
        UdpClient GetTcpClient(int port);
        UdpClient GetUdpClient(int port);
        void Stop();
    }

    internal class ClientsService : IClientsService {
        readonly ILogger logger;
        readonly List<BaseClient> tcpClients = new();
        readonly List<UdpClient> udpClients = new();

        public ClientsService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public void Start(IEnumerable<int>? tcpPorts, IEnumerable<int>? udpPorts) {
            Stop();

            if(udpPorts != null) {
                udpClients.AddRange(udpPorts.Select(x => new UdpClient(new IPEndPoint(IPAddress.Loopback, x), logger)));
            }
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
