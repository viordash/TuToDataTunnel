using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using Serilog;
using TutoProxy.Core.Models;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Hubs;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Tests.Services {
    public class ClientsServiceTests {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        class TestableClientsService : ClientsService {
            public TestableClientsService(ILogger logger, IHostApplicationLifetime applicationLifetime,
            IRequestProcessingService requestProcessingService,
            IPEndPoint localEndPoint,
            List<int>? alowedTcpPorts,
            List<int>? alowedUdpPorts)
                : base(logger, applicationLifetime, requestProcessingService, localEndPoint, alowedTcpPorts, alowedUdpPorts) {
            }

            public ConcurrentDictionary<string, Client> PublicMorozovConnectedClients {
                get { return connectedClients; }
            }
        }

        TestableClientsService testable;

        Mock<ILogger> loggerMock;
        Mock<IHostApplicationLifetime> applicationLifetimeMock;
        Mock<IRequestProcessingService> requestProcessingServiceMock;
        Mock<IClientProxy> clientProxyMock;
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Loopback, 0);


        string? clientsRequest;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            applicationLifetimeMock = new();
            requestProcessingServiceMock = new();
            clientProxyMock = new();

            clientsRequest = null;
            clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, args, cancellationToken) => {
                    clientsRequest = args[0] as string;
                });


        }

        [Test]
        public async Task Clients_WithAlready_Used_TcpPort_Are_Rejected_Test() {
            testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, requestProcessingServiceMock.Object, localEndPoint, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1, 65535).ToList());

            await testable.ConnectAsync("connectionId0", clientProxyMock.Object, "tcpquery=80,81,443");
            await testable.ConnectAsync("connectionId1", clientProxyMock.Object, "tcpquery=180,181,1443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);

            await testable.ConnectAsync("connectionId2", clientProxyMock.Object, "tcpquery=80");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.Is<object?[]>(a => a.Length > 0 && (a[0] as string)!.Contains("tcp ports already in us")),
                It.IsAny<CancellationToken>()), Times.Once);

            await testable.ConnectAsync("connectionId3", clientProxyMock.Object, "tcpquery=180,181,1443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.Is<object?[]>(a => a.Length > 0 && (a[0] as string)!.Contains("tcp ports already in us")),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public async Task Clients_WithAlready_Used_UdpPort_Are_Rejected_Test() {
            testable = new TestableClientsService(loggerMock.Object, applicationLifetimeMock.Object, requestProcessingServiceMock.Object, localEndPoint, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1, 65535).ToList());
            await testable.ConnectAsync("connectionId0", clientProxyMock.Object, "udpquery=1080,1081,10443");
            await testable.ConnectAsync("connectionId1", clientProxyMock.Object, "udpquery=10180,10181,11443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);

            await testable.ConnectAsync("connectionId2", clientProxyMock.Object, "udpquery=1080");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.Is<object?[]>(a => a.Length > 0 && (a[0] as string)!.Contains("udp ports already in us")),
                It.IsAny<CancellationToken>()), Times.Once);

            await testable.ConnectAsync("connectionId3", clientProxyMock.Object, "udpquery=10180,10181,11443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.Is<object?[]>(a => a.Length > 0 && (a[0] as string)!.Contains("udp ports already in us")),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

    }
}
