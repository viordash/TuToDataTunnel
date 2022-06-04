using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using TutoProxy.Core.CommandLine;
using TutoProxy.Server.Communication;
using TuToProxy.Core;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Server.Services {
    public interface IHubClientsService : IDisposable {
        void Connect(string connectionId, IClientProxy clientProxy, string? queryString);
        void Disconnect(string connectionId);
        Client? GetUdpClient(int port);
    }

    public class HubClientsService : IHubClientsService {
        readonly ILogger logger;
        protected readonly ConcurrentDictionary<string, Client> connectedClients = new();
        readonly IHostApplicationLifetime applicationLifetime;
        readonly IServiceProvider serviceProvider;
        readonly IPEndPoint localEndPoint;
        readonly IEnumerable<int>? alowedTcpPorts;
        readonly IEnumerable<int>? alowedUdpPorts;

        public HubClientsService(
            ILogger logger,
            IHostApplicationLifetime applicationLifetime,
            IServiceProvider serviceProvider,
            IPEndPoint localEndPoint,
            IEnumerable<int>? alowedTcpPorts,
            IEnumerable<int>? alowedUdpPorts) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(applicationLifetime, nameof(applicationLifetime));
            Guard.NotNull(serviceProvider, nameof(serviceProvider));
            Guard.NotNull(localEndPoint, nameof(localEndPoint));
            Guard.NotNull(alowedTcpPorts ?? alowedUdpPorts, "alowedTcpPorts ?? alowedUdpPorts");
            this.logger = logger;
            this.applicationLifetime = applicationLifetime;
            this.serviceProvider = serviceProvider;
            this.localEndPoint = localEndPoint;
            this.alowedTcpPorts = alowedTcpPorts;
            this.alowedUdpPorts = alowedUdpPorts;
        }

        public void Connect(string connectionId, IClientProxy clientProxy, string? queryString) {
            if(queryString == null) {
                throw new ClientConnectionException(connectionId, "QueryString empty");
            }
            var query = QueryHelpers.ParseQuery(queryString);
            var tcpPresent = query.TryGetValue(SignalRParams.TcpQuery, out StringValues tcpQuery);
            var udpPresent = query.TryGetValue(SignalRParams.UdpQuery, out StringValues udpQuery);

            if(!tcpPresent && !udpPresent) {
                throw new ClientConnectionException(connectionId, "tcp or udp options requried");
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
                throw new ClientConnectionException(connectionId, "tcp or udp options error");
            }


            var bannedTcpPorts = GetBannedPorts(alowedTcpPorts, tcpPorts);
            if(bannedTcpPorts.Any()) {
                var message = $"banned tcp ports [{string.Join(",", bannedTcpPorts)}]";
                throw new ClientConnectionException(connectionId, message);
            }

            var bannedUdpPorts = GetBannedPorts(alowedUdpPorts, udpPorts);
            if(bannedUdpPorts.Any()) {
                var message = $"banned udp ports [{string.Join(",", bannedUdpPorts)}]";
                throw new ClientConnectionException(connectionId, message);
            }

            var clients = connectedClients.Values.ToList();

            var alreadyUsedTcpPorts = GetAlreadyUsedTcpPorts(clients, tcpPorts);
            if(alreadyUsedTcpPorts.Any()) {
                var message = $"tcp ports already in use [{string.Join(",", alreadyUsedTcpPorts)}]";
                throw new ClientConnectionException(connectionId, message);
            }

            var alreadyUsedUdpPorts = GetAlreadyUsedUdpPorts(clients, udpPorts);
            if(alreadyUsedUdpPorts.Any()) {
                var message = $"udp ports already in use [{string.Join(",", alreadyUsedUdpPorts)}]";
                throw new ClientConnectionException(connectionId, message);
            }

            var client = new Client(localEndPoint, clientProxy, tcpPorts, udpPorts, serviceProvider);
            if(connectedClients.TryAdd(connectionId, client)) {
                logger.Information($"Connect client :{connectionId} (tcp:{tcpQuery}, udp:{udpQuery})");
                client.Listen();
            }
        }

        public void Disconnect(string connectionId) {
            logger.Information($"Disconnect client :{connectionId}");
            if(connectedClients.TryRemove(connectionId, out Client? client)) {
                client.Dispose();
            }
        }

        IEnumerable<int> GetBannedPorts(IEnumerable<int>? allowedPorts, IEnumerable<int>? ports) {
            if(allowedPorts != null && ports != null) {
                var bannedPorts = ports
                .Where(x => !allowedPorts.Contains(x));
                return bannedPorts;
            }
            return Enumerable.Empty<int>();
        }

        IEnumerable<int> GetAlreadyUsedTcpPorts(List<Client> clients, IEnumerable<int>? tcpPorts) {
            if(tcpPorts != null) {
                var alreadyUsedPorts = clients
                .Where(x => x.TcpPorts != null)
                .SelectMany(x => x.TcpPorts!)
                .Intersect(tcpPorts);
                return alreadyUsedPorts;
            }
            return Enumerable.Empty<int>();
        }

        IEnumerable<int> GetAlreadyUsedUdpPorts(List<Client> clients, IEnumerable<int>? udpPorts) {
            if(udpPorts != null) {
                var alreadyUsedPorts = clients
                .Where(x => x.UdpPorts != null)
                .SelectMany(x => x.UdpPorts!)
                .Intersect(udpPorts);
                return alreadyUsedPorts;
            }
            return Enumerable.Empty<int>();
        }

        public void Dispose() {
            var clients = connectedClients.Values.ToList();
            foreach(var client in clients) {
                client.Dispose();
            }
        }

        public Client? GetUdpClient(int port) {
            var clients = connectedClients.Values.ToList();
            return clients.FirstOrDefault(x => x.UdpPorts?.Contains(port) == true);
        }
    }
}
