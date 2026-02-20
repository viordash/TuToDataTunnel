using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using TutoProxy.Server.Hubs;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Models;

namespace TutoProxy.Server.Tests.Services {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public class DataTransferServiceTests {
        DataTransferService testable;

        Mock<IClientHub> typedClientMock;
        Mock<IHubClientsService> clientsServiceMock;

        [SetUp]
        public void Setup() {
            var loggerMock = new Mock<ILogger>();
            var typedHubContextMock = new Mock<IHubContext<SignalRHub, IClientHub>>();
            var rawHubContextMock = new Mock<IHubContext<SignalRHub>>();
            var typedHubClientsMock = new Mock<IHubClients<IClientHub>>();
            typedClientMock = new();
            clientsServiceMock = new();

            typedHubClientsMock
                .Setup(x => x.Client(It.IsAny<string>()))
                .Returns(() => typedClientMock.Object);

            typedHubContextMock
                .SetupGet(x => x.Clients)
                .Returns(() => typedHubClientsMock.Object);

            clientsServiceMock
                .Setup(x => x.GetConnectionIdForUdp(It.IsAny<int>()))
                .Returns<int>((port) => $"udp-{port}");

            testable = new DataTransferService(loggerMock.Object, typedHubContextMock.Object, rawHubContextMock.Object, clientsServiceMock.Object);
        }

        [Test]
        public async Task SendUdpRequest_Test() {
            var requestModel = new UdpDataRequestModel() { Port = 700, OriginPort = 800, Data = Array.Empty<byte>() };
            await testable.SendUdpRequest(requestModel, new CancellationTokenSource().Token);
            typedClientMock.Verify(x => x.UdpRequest(It.Is<UdpDataRequestModel>(r => r.Port == 700)), Times.Once);
        }
    }
}
