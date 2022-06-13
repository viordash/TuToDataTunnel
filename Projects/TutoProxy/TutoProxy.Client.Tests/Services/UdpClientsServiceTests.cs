﻿using System.Collections.Concurrent;
using System.Net;
using Serilog;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;
using TutoProxy.Core.Models;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Tests.Services {
    public class UdpClientsServiceTests {

        public class TestableUdpClient : UdpClient {

            public TestableUdpClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, Action<int, int> timeoutAction)
                : base(serverEndPoint, originPort, logger, timeoutAction) {
            }

            protected override TimeSpan ReceiveTimeout { get { return TimeSpan.FromMilliseconds(1000); } }

            protected override System.Net.Sockets.UdpClient CreateSocket() {
                return new System.Net.Sockets.UdpClient();
            }
        }

        class TestableClientsService : ClientsService {
            public TestableClientsService(ILogger logger, IClientFactory clientFactory)
                : base(logger, clientFactory) {
            }

            public ConcurrentDictionary<int, ConcurrentDictionary<int, UdpClient>> PublicMorozovUdpClients {
                get { return udpClients; }
            }
        }

        Mock<ILogger> loggerMock;
        Mock<IClientFactory> clientFactoryMock;

        TestableClientsService testable;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            clientFactoryMock = new();

            clientFactoryMock
                .Setup(x => x.Create(It.IsAny<IPAddress>(), It.IsAny<UdpDataRequestModel>(), It.IsAny<Action<int, int>>()))
                .Returns<IPAddress, UdpDataRequestModel, Action<int, int>>((localIpAddress, request, timeoutAction) => {
                    return new TestableUdpClient(new IPEndPoint(localIpAddress, request.Port), request.OriginPort, loggerMock.Object, timeoutAction);
                });

            testable = new TestableClientsService(loggerMock.Object, clientFactoryMock.Object);
        }

        [Test]
        public void ObtainUdpClient_With_Banned_UdpPort_Are_Throws_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            Assert.Throws<ClientNotFoundException>(() => testable.ObtainClient(new UdpDataRequestModel(999, 50999, new byte[] { 0, 1 })));
            Assert.Throws<ClientNotFoundException>(() => testable.ObtainClient(new UdpDataRequestModel(1005, 51005, new byte[] { 0, 1 })));
        }

        [Test]
        public void ObtainUdpClient_Only_Once_Creating_List_With_Same_Port_Clients_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            var client0 = testable.ObtainClient(new UdpDataRequestModel(1000, 51000, new byte[] { 0, 1 }));
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000][51000], Is.SameAs(client0));

            var client1 = testable.ObtainClient(new UdpDataRequestModel(1000, 51001, new byte[] { 0, 1 }));
            Assert.IsNotNull(client1);
            Assert.That(client1.Port, Is.EqualTo(1000));
            Assert.That(client1.OriginPort, Is.EqualTo(51001));

            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000, 51001 }));
            Assert.That(testable.PublicMorozovUdpClients[1000][51001], Is.SameAs(client1));
        }

        [Test]
        public void ObtainUdpClient_Only_Once_Creating_Same_Port_Client_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            var client0 = testable.ObtainClient(new UdpDataRequestModel(1000, 51000, new byte[] { 0, 1 }));
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            var client1 = testable.ObtainClient(new UdpDataRequestModel(1000, 51000, new byte[] { 0, 1 }));
            Assert.That(client1, Is.SameAs(client0));

            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000][51000], Is.SameAs(client0));
        }

        [Test]
        public async Task UdpClients_Is_Auto_Removed_After_Timeout_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 50).ToList());

            for(int port = 0; port < 50; port++) {
                for(int origPort = 0; origPort < 10; origPort++) {
                    Assert.IsNotNull(testable.ObtainClient(new UdpDataRequestModel(1000 + port, 51000 + origPort, new byte[] { 0, 1 })));
                }
            }
            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(Enumerable.Range(1000, 50)));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(Enumerable.Range(51000, 10)));

            await Task.Delay(1100);
            for(int i = 0; i < 50; i++) {
                Assert.That(testable.PublicMorozovUdpClients[1000 + i].Keys, Is.Empty);
            }
        }

        [Test]
        public async Task UdpClient_Timeout_Timer_Is_Refreshed_During_Obtaining_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 1).ToList());

            Assert.IsNotNull(testable.ObtainClient(new UdpDataRequestModel(1000, 51000, new byte[] { 0, 1 })));

            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));

            await Task.Delay(500);
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));

            Assert.IsNotNull(testable.ObtainClient(new UdpDataRequestModel(1000, 51000, new byte[] { 0, 1 })));
            await Task.Delay(500);
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));

            Assert.IsNotNull(testable.ObtainClient(new UdpDataRequestModel(1000, 51000, new byte[] { 0, 1 })));
            await Task.Delay(500);
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            await Task.Delay(600);

            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.Empty);

        }
    }
}