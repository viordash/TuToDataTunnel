using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
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
            public TestableClientsService(ILogger logger)
                : base(logger) {
            }

            public ConcurrentDictionary<string, Client> PublicMorozovConnectedClients {
                get { return connectedClients; }
            }
        }

        TestableClientsService testable;

        Mock<IClientProxy> clientProxyMock;


        string? clientsRequest;

        [SetUp]
        public void Setup() {
            var loggerMock = new Mock<ILogger>();
            clientProxyMock = new();

            clientsRequest = null;
            clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, args, cancellationToken) => {
                    clientsRequest = args[0] as string;
                });

            testable = new TestableClientsService(loggerMock.Object);
        }

        [Test]
        public void Clients_WithAlready_Used_TcpPort_Are_Rejected_Test() {
            testable.Connect("connectionId0", clientProxyMock.Object, "tcpquery=80,81,443");
            testable.Connect("connectionId1", clientProxyMock.Object, "tcpquery=180,181,1443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);

            testable.Connect("connectionId2", clientProxyMock.Object, "tcpquery=80");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.Is<object?[]>(a => a.Length > 0 && (a[0] as string)!.Contains("tcp ports already in us")),
                It.IsAny<CancellationToken>()), Times.Once);

            testable.Connect("connectionId3", clientProxyMock.Object, "tcpquery=180,181,1443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.Is<object?[]>(a => a.Length > 0 && (a[0] as string)!.Contains("tcp ports already in us")),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public void Clients_WithAlready_Used_UdpPort_Are_Rejected_Test() {
            testable.Connect("connectionId0", clientProxyMock.Object, "udpquery=1080,1081,10443");
            testable.Connect("connectionId1", clientProxyMock.Object, "udpquery=10180,10181,11443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));

            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);

            testable.Connect("connectionId2", clientProxyMock.Object, "udpquery=1080");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.Is<object?[]>(a => a.Length > 0 && (a[0] as string)!.Contains("udp ports already in us")),
                It.IsAny<CancellationToken>()), Times.Once);

            testable.Connect("connectionId3", clientProxyMock.Object, "udpquery=10180,10181,11443");
            Assert.That(testable.PublicMorozovConnectedClients.Keys, Is.EquivalentTo(new[] { "connectionId0", "connectionId1" }));
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "Errors"), It.Is<object?[]>(a => a.Length > 0 && (a[0] as string)!.Contains("udp ports already in us")),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

    }
}
