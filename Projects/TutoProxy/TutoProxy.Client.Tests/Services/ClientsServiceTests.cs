using System.Collections.Concurrent;
using System.Net;
using Serilog;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;
using TutoProxy.Core.Models;
using TuToProxy.Core.Exceptions;

namespace TutoProxy.Client.Tests.Services {
    public class ClientsServiceTests {

        class TestableClientsService : ClientsService {
            public TestableClientsService(ILogger logger)
                : base(logger) {
            }

            public ConcurrentDictionary<int, ConcurrentDictionary<int, UdpClient>> PublicMorozovUdpClients {
                get { return udpClients; }
            }
        }

        Mock<ILogger> loggerMock;

        TestableClientsService testable;

        [SetUp]
        public void Setup() {
            loggerMock = new();

            testable = new TestableClientsService(loggerMock.Object);
        }

        [Test]
        public void ObtainUdpClient_With_Banned_UdpPort_Are_Throws_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            Assert.Throws<ClientNotFoundException>(() => testable.ObtainUdpClient(new UdpDataRequestModel(999, 50999, new byte[] { 0, 1 })));
            Assert.Throws<ClientNotFoundException>(() => testable.ObtainUdpClient(new UdpDataRequestModel(1005, 51005, new byte[] { 0, 1 })));
        }

        [Test]
        public void ObtainUdpClient_Only_Once_Creating_List_With_Same_Port_Clients_Test() {
            testable.Start(IPAddress.Any, Enumerable.Range(1, 65535).ToList(), Enumerable.Range(1000, 4).ToList());

            var client0 = testable.ObtainUdpClient(new UdpDataRequestModel(1000, 51000, new byte[] { 0, 1 }));
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000][51000], Is.SameAs(client0));

            var client1 = testable.ObtainUdpClient(new UdpDataRequestModel(1000, 51001, new byte[] { 0, 1 }));
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

            var client0 = testable.ObtainUdpClient(new UdpDataRequestModel(1000, 51000, new byte[] { 0, 1 }));
            Assert.IsNotNull(client0);
            Assert.That(client0.Port, Is.EqualTo(1000));
            Assert.That(client0.OriginPort, Is.EqualTo(51000));

            var client1 = testable.ObtainUdpClient(new UdpDataRequestModel(1000, 51000, new byte[] { 0, 1 }));
            Assert.That(client1, Is.SameAs(client0));

            Assert.That(testable.PublicMorozovUdpClients.Keys, Is.EquivalentTo(new[] { 1000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000].Keys, Is.EquivalentTo(new[] { 51000 }));
            Assert.That(testable.PublicMorozovUdpClients[1000][51000], Is.SameAs(client0));
        }
    }
}
