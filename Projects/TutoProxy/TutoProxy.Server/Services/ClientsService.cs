using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using TutoProxy.Core.CommandLine;
using TutoProxy.Server.Communication;
using TuToProxy.Core;

namespace TutoProxy.Server.Services {
    public interface IClientsService {
        void Connect(string connectionId, IClientProxy clientProxy, string queryString);
        void Disconnect(string connectionId);
        Task SendAsync(IClientProxy clientProxy, string method, object? arg1, CancellationToken cancellationToken = default);
    }

    public class ClientsService : IClientsService {
        readonly ILogger logger;
        protected readonly ConcurrentDictionary<string, Client> connectedClients = new();

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

            if(tcpPorts == null && udpPorts == null) {
                clientProxy.SendAsync("Errors", "tcp or udp options requried");
                return;
            }

            var clients = connectedClients.Values.ToList();

            var alreadyUsedTcpPorts = GetAlreadyUsedTcpPorts(clients, tcpPorts);
            if(alreadyUsedTcpPorts.Any()) {
                clientProxy.SendAsync("Errors", $"tcp ports already in use [{string.Join(",", alreadyUsedTcpPorts)}]");
                return;
            }

            var alreadyUsedUdpPorts = GetAlreadyUsedUdpPorts(clients, udpPorts);
            if(alreadyUsedUdpPorts.Any()) {
                clientProxy.SendAsync("Errors", $"udp ports already in use [{string.Join(",", alreadyUsedUdpPorts)}]");
                return;
            }

            connectedClients.TryAdd(connectionId, new Client(clientProxy, tcpPorts, udpPorts));
            logger.Information($"Connect client :{connectionId} (tcp:{tcpQuery}, udp:{udpQuery})");
        }

        public void Disconnect(string connectionId) {
            logger.Information($"Disconnect client :{connectionId}");
            connectedClients.TryRemove(connectionId, out Client? client);
        }

        public Task SendAsync(IClientProxy clientProxy, string method, object? arg1, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        IEnumerable<int> GetAlreadyUsedTcpPorts(List<Client> clients, List<int>? tcpPorts) {
            if(tcpPorts != null) {
                var alreadyUsedPorts = clients
                .Where(x => x.TcpPorts != null)
                .SelectMany(x => x.TcpPorts!)
                .Intersect(tcpPorts);
                return alreadyUsedPorts;
            }
            return Enumerable.Empty<int>();
        }

        IEnumerable<int> GetAlreadyUsedUdpPorts(List<Client> clients, List<int>? udpPorts) {
            if(udpPorts != null) {
                var alreadyUsedPorts = clients
                .Where(x => x.UdpPorts != null)
                .SelectMany(x => x.UdpPorts!)
                .Intersect(udpPorts);
                return alreadyUsedPorts;
            }
            return Enumerable.Empty<int>();
        }
    }
}
