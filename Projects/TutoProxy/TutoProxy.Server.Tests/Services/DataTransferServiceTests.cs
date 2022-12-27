using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using TutoProxy.Server.Hubs;
using TutoProxy.Server.Services;
using TuToProxy.Core.Models;

namespace TutoProxy.Server.Tests.Services {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public class DataTransferServiceTests {
        DataTransferService testable;

        Mock<ISingleClientProxy> clientProxyMock;
        Mock<IHubClientsService> clientsServiceMock;

        UdpDataRequestModel? sendedTransferUdpRequest;

        [SetUp]
        public void Setup() {
            var loggerMock = new Mock<ILogger>();
            var hubContextMock = new Mock<IHubContext<SignalRHub>>();
            var hubClientsMock = new Mock<IHubClients>();
            clientProxyMock = new();
            clientsServiceMock = new();

            hubClientsMock
                .SetupGet(x => x.All)
                .Returns(() => clientProxyMock.Object);

            hubClientsMock
                .Setup(x => x.Client(It.IsAny<string>()))
                .Returns(() => clientProxyMock.Object);

            hubContextMock
                .SetupGet(x => x.Clients)
                .Returns(() => hubClientsMock.Object);

            sendedTransferUdpRequest = null;
            clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, args, cancellationToken) => {
                    sendedTransferUdpRequest = args[0] as UdpDataRequestModel;
                });

            clientsServiceMock
                .Setup(x => x.GetConnectionIdForUdp(It.IsAny<int>()))
                .Returns<int>((port) => $"udp-{port}");

            testable = new DataTransferService(loggerMock.Object, hubContextMock.Object, clientsServiceMock.Object);
        }

        [Test]
        public async Task SendUdpRequest_Test() {
            var requestModel = new UdpDataRequestModel() { Port = 700, OriginPort = 800, Data = Array.Empty<byte>() };
            await testable.SendUdpRequest(requestModel, new CancellationTokenSource().Token);
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "UdpRequest"), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
