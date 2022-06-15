using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;
using TutoProxy.Core.Models;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Services;
using TuToProxy.Core.Exceptions;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Tests.Services {
    public class HubClientTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        class TestableClientsService : HubClientsService {
            public TestableClientsService(ILogger logger, IHostApplicationLifetime applicationLifetime,
            IServiceProvider serviceProvider,
            IPEndPoint localEndPoint,
            IEnumerable<int>? alowedTcpPorts,
            IEnumerable<int>? alowedUdpPorts)
                : base(logger, applicationLifetime, serviceProvider, localEndPoint, alowedTcpPorts, alowedUdpPorts) {
            }

            public ConcurrentDictionary<string, HubClient> PublicMorozovConnectedClients {
                get { return connectedClients; }
            }
        }


        Mock<ILogger> loggerMock;
        Mock<IServiceProvider> serviceProviderMock;
        Mock<IClientProxy> clientProxyMock;
        Mock<IDataTransferService> dataTransferServiceMock;
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        DateTime nowDateTime;
        string? clientsRequest;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            serviceProviderMock = new();
            clientProxyMock = new();
            dataTransferServiceMock = new();

            clientsRequest = null;
            clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, args, cancellationToken) => {
                    clientsRequest = args[0] as string;
                });

            serviceProviderMock
                .Setup(x => x.GetService(It.IsAny<Type>()))
                .Returns<Type>((type) => {
                    return type switch {
                        _ when type == typeof(IDataTransferService) => dataTransferServiceMock.Object,
                        _ when type == typeof(ILogger) => loggerMock.Object,
                        _ => null
                    };
                });

        }

        [Test]
        public void SendTcpResponse_Throws_SocketPortNotBoundException_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(), Enumerable.Range(1000, 4), serviceProviderMock.Object);

            Assert.ThrowsAsync<SocketPortNotBoundException>(async () => await testable.SendTcpResponse(new TcpDataResponseModel(11, 10000, new byte[] { 0, 1 })),
                    "Tcp socket port(11) not bound");
        }

        [Test]
        public void SendUdpResponse_Throws_SocketPortNotBoundException_Test() {
            using var testable = new HubClient(localEndPoint, clientProxyMock.Object, Enumerable.Range(1, 10).ToList(), Enumerable.Range(1000, 4), serviceProviderMock.Object);

            Assert.ThrowsAsync<SocketPortNotBoundException>(async () => await testable.SendUdpResponse(new UdpDataResponseModel(11, 10000, new byte[] { 0, 1 })),
                    "Udp socket port(11) not bound");
        }
    }
}
