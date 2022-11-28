using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;

namespace TutoProxy.Client.Tests.Communication {
    public class BaseClientTests {

        public class TestableClient : BaseClient<Socket> {
            public bool Timeout = false;


            public TestableClient(IPEndPoint serverEndPoint, int originPort, ILogger logger, IClientsService clientsService, ISignalRClient dataTunnelClient)
                : base(serverEndPoint, originPort, logger, clientsService, dataTunnelClient) {
            }

            protected override Socket CreateSocket() {
                return new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }

        }


        Mock<ILogger> loggerMock;
        Mock<IClientsService> clientsServiceMock;
        Mock<ISignalRClient> signalRClientMock;

        [SetUp]
        public void Setup() {
            loggerMock = new();
            clientsServiceMock = new();
            signalRClientMock = new();
        }

        [Test]
        public async Task TimeoutAction_Test() {
            var testable = new TestableClient(new IPEndPoint(IPAddress.Any, 700), 100, loggerMock.Object, clientsServiceMock.Object, signalRClientMock.Object);

            await Task.Delay(200);
            Assert.That(testable.Timeout, Is.False);

            await Task.Delay(300);
            Assert.That(testable.Timeout, Is.True);
        }

    }
}
