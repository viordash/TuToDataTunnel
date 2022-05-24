using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using TutoProxy.Core.CommandLine;
using TuToProxy.Core;

namespace TutoProxy.Server.Services {
    public interface IClientsService {
        void Connect(string connectionId, IClientProxy clientProxy, string queryString);
        void Disconnect(string connectionId);
        Task SendAsync(IClientProxy clientProxy, string method, object? arg1, CancellationToken cancellationToken = default);
    }

    public class ClientsService : IClientsService {
        #region inner classes
        public class Client {
            public IClientProxy ClientProxy { get; private set; }
            public List<int>? TcpPorts { get; private set; }
            public List<int>? UdpPorts { get; private set; }
            public Client(IClientProxy clientProxy, List<int>? tcpPorts, List<int>? udpPorts) {
                ClientProxy = clientProxy;
                TcpPorts = tcpPorts;
                UdpPorts = udpPorts;
            }
        }
        #endregion

        readonly ILogger logger;
        protected readonly ConcurrentDictionary<string, Client> clients = new();

        public ClientsService(
            ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public void Connect(string connectionId, IClientProxy clientProxy, string queryString) {

            var query = QueryHelpers.ParseQuery(queryString);
            var tcpPresent = query.TryGetValue(DataTunnelParams.TcpQuery, out StringValues tcpQuery);
            var udpPresent = query.TryGetValue(DataTunnelParams.UdpQuery, out StringValues udpQuery);

            if(!tcpPresent && !udpPresent) {
                clientProxy.SendAsync("Errors", "tcp or udp options requried");
                return;
            }

            List<int>? tcpPorts = null;
            if(!string.IsNullOrEmpty(tcpQuery)) {
                tcpPorts = PortsArgument.Parse(tcpQuery).Ports;
            }

            List<int>? udpPorts = null;
            if(!string.IsNullOrEmpty(udpQuery)) {
                udpPorts = PortsArgument.Parse(udpQuery).Ports;
            }

            if(tcpPorts != null && udpPorts == null) {
                clientProxy.SendAsync("Errors", "tcp or udp options requried");
                return;
            }
            clients.TryAdd(connectionId, new Client(clientProxy, tcpPorts, udpPorts));
            logger.Information($"Connect client :{connectionId} (tcp:{tcpQuery}, udp:{udpQuery})");
        }

        public void Disconnect(string connectionId) {
            logger.Information($"Disconnect client :{connectionId}");
            clients.TryRemove(connectionId, out Client? client);
        }

        public Task SendAsync(IClientProxy clientProxy, string method, object? arg1, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }
    }
}
