using System.Net;
using System.Net.Sockets;
using Serilog;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;

namespace TutoProxy.Client.Tests.Communication {
    public class BaseClientTests {

        public class TestableClient : BaseClient<Socket> {

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

    }
}
