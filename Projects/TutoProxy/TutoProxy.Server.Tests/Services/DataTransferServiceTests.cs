using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using NUnit.Framework;
using Serilog;
using TutoProxy.Core.Models;
using TutoProxy.Server.Hubs;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Tests.Services {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public class DataTransferServiceTests {
        class TestableDataTransferService : DataTransferService {
            public TestableDataTransferService(ILogger logger, IIdService idService, IDateTimeService dateTimeService, IHubContext<DataTunnelHub> hubContext)
                : base(logger, idService, dateTimeService, hubContext) {
            }

            public ConcurrentDictionary<string, NamedRequest> PublicMorozovRequests {
                get { return requests; }
            }
        }

        TestableDataTransferService testable;

        Mock<IClientProxy> clientProxyMock;

        DateTime nowDateTime;
        string requestId = string.Empty;
        TransferRequestModel? sendedTransferRequest;

        [SetUp]
        public void Setup() {
            var loggerMock = new Mock<ILogger>();
            var hubContextMock = new Mock<IHubContext<DataTunnelHub>>();
            var idServiceMock = new Mock<IIdService>();
            var dateTimeServiceMock = new Mock<IDateTimeService>();
            var hubClientsMock = new Mock<IHubClients>();
            clientProxyMock = new();

            hubClientsMock
                .SetupGet(x => x.All)
                .Returns(() => clientProxyMock.Object);

            hubContextMock
                .SetupGet(x => x.Clients)
                .Returns(() => hubClientsMock.Object);

            requestId = "requestId";
            idServiceMock
                .SetupGet(x => x.TransferRequest)
                .Returns(() => requestId);

            nowDateTime = DateTime.Now;
            dateTimeServiceMock
                .SetupGet(x => x.Now)
                .Returns(() => nowDateTime);

            sendedTransferRequest = null;
            clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, args, cancellationToken) => {
                    sendedTransferRequest = args[0] as TransferRequestModel;
                });

            testable = new TestableDataTransferService(loggerMock.Object, idServiceMock.Object, dateTimeServiceMock.Object, hubContextMock.Object);
        }

        [Test]
        public async Task RemoveExpiredRequests_Test() {
            DataResponseModel? responseFromCallback = null;

            var requestModel = new UdpDataRequestModel();
            requestId = Guid.NewGuid().ToString();
            await testable.SendRequest(requestModel, (response) => { responseFromCallback = response; });
            Assert.That(testable.PublicMorozovRequests.Keys, Has.Count.EqualTo(1));

            nowDateTime = nowDateTime.AddSeconds(60);
            requestId = Guid.NewGuid().ToString();
            await testable.SendRequest(requestModel, (response) => { responseFromCallback = response; });
            Assert.That(testable.PublicMorozovRequests.Keys, Has.Count.EqualTo(2));

            nowDateTime = nowDateTime.AddSeconds(61);
            requestId = Guid.NewGuid().ToString();
            await testable.SendRequest(requestModel, (response) => { responseFromCallback = response; });
            Assert.That(testable.PublicMorozovRequests.Keys, Has.Count.EqualTo(1));

            Assert.That(responseFromCallback, Is.Null);
            Assert.That(sendedTransferRequest, Is.Not.Null);
#pragma warning disable CS8604 // Possible null reference argument.
            testable.ReceiveResponse(new TransferResponseModel(sendedTransferRequest, new UdpDataResponseModel() { Data = new byte[] { 1, 2, 3, 4 } }));
#pragma warning restore CS8604 // Possible null reference argument.

            Assert.That(responseFromCallback, Is.Not.Null);
            Assert.That(responseFromCallback?.Data, Is.EquivalentTo(new byte[] { 1, 2, 3, 4 }));

            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "DataRequest"), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Test]
        public async Task ReceiveResponse_Is_Single_Action_Test() {
            DataResponseModel? responseFromCallback = null;

            var requestModel = new UdpDataRequestModel();
            await testable.SendRequest(requestModel, (response) => { responseFromCallback = response; });

            Assert.That(testable.PublicMorozovRequests.Keys, Has.Count.EqualTo(1));
#pragma warning disable CS8604 // Possible null reference argument.
            testable.ReceiveResponse(new TransferResponseModel(sendedTransferRequest, new UdpDataResponseModel() { Data = new byte[] { 1, 2, 3, 4 } }));
#pragma warning restore CS8604 // Possible null reference argument.

            Assert.That(testable.PublicMorozovRequests.Keys, Has.Count.EqualTo(0));
            Assert.That(responseFromCallback, Is.Not.Null);
            Assert.That(responseFromCallback?.Data, Is.EquivalentTo(new byte[] { 1, 2, 3, 4 }));

            responseFromCallback = null;
            testable.ReceiveResponse(new TransferResponseModel(sendedTransferRequest, new UdpDataResponseModel() { Data = new byte[] { 1, 2, 3, 4 } }));
            Assert.That(responseFromCallback, Is.Null);
            clientProxyMock.Verify(x => x.SendCoreAsync(It.Is<string>(m => m == "DataRequest"), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
