﻿using System.Collections.Concurrent;
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

namespace TutoProxy.Server.Services {
    public interface IClientsService : IDisposable {
        Task ConnectAsync(string connectionId, IClientProxy clientProxy, string queryString);
        void Disconnect(string connectionId);
        Task SendAsync(IClientProxy clientProxy, string method, object? arg1, CancellationToken cancellationToken = default);
    }

    public class ClientsService : IClientsService {
        readonly ILogger logger;
        protected readonly ConcurrentDictionary<string, Client> connectedClients = new();
        readonly IHostApplicationLifetime applicationLifetime;
        readonly IRequestProcessingService requestProcessingService;
        readonly IPEndPoint localEndPoint;
        readonly List<int>? alowedTcpPorts;
        readonly List<int>? alowedUdpPorts;

        public ClientsService(
            ILogger logger,
            IHostApplicationLifetime applicationLifetime,
            IRequestProcessingService requestProcessingService,
            IPEndPoint localEndPoint,
            List<int> alowedTcpPorts,
            List<int> alowedUdpPorts) {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(applicationLifetime, nameof(applicationLifetime));
            Guard.NotNull(requestProcessingService, nameof(requestProcessingService));
            Guard.NotNull(localEndPoint, nameof(localEndPoint));
            Guard.NotNull(alowedTcpPorts ?? alowedUdpPorts, "alowedTcpPorts ?? alowedUdpPorts");
            this.logger = logger;
            this.applicationLifetime = applicationLifetime;
            this.requestProcessingService = requestProcessingService;
            this.localEndPoint = localEndPoint;
            this.alowedTcpPorts = alowedTcpPorts;
            this.alowedUdpPorts = alowedUdpPorts;
        }

        public async Task ConnectAsync(string connectionId, IClientProxy clientProxy, string queryString) {
            var query = QueryHelpers.ParseQuery(queryString);
            var tcpPresent = query.TryGetValue(DataTunnelParams.TcpQuery, out StringValues tcpQuery);
            var udpPresent = query.TryGetValue(DataTunnelParams.UdpQuery, out StringValues udpQuery);

            if(!tcpPresent && !udpPresent) {
                await clientProxy.SendAsync("Errors", "tcp or udp options requried", applicationLifetime.ApplicationStopping);
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
                await clientProxy.SendAsync("Errors", "tcp or udp options requried", applicationLifetime.ApplicationStopping);
                return;
            }

            var clients = connectedClients.Values.ToList();

            var alreadyUsedTcpPorts = GetAlreadyUsedTcpPorts(clients, tcpPorts);
            if(alreadyUsedTcpPorts.Any()) {
                await clientProxy.SendAsync("Errors", $"tcp ports already in use [{string.Join(",", alreadyUsedTcpPorts)}]", applicationLifetime.ApplicationStopping);
                return;
            }

            var alreadyUsedUdpPorts = GetAlreadyUsedUdpPorts(clients, udpPorts);
            if(alreadyUsedUdpPorts.Any()) {
                await clientProxy.SendAsync("Errors", $"udp ports already in use [{string.Join(",", alreadyUsedUdpPorts)}]", applicationLifetime.ApplicationStopping);
                return;
            }

            var client = new Client(localEndPoint, clientProxy, tcpPorts, udpPorts, logger, requestProcessingService);
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

        public void Dispose() {
            var clients = connectedClients.Values.ToList();
            foreach(var client in clients) {
                client.Dispose();
            }
        }
    }
}
