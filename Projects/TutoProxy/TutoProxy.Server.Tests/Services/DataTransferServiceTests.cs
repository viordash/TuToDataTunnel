using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public class DataTransferServiceTests {
        class TestableDataTransferService : DataTransferService {
            public TestableDataTransferService(ILogger logger, IIdService idService, IDateTimeService dateTimeService, IHubContext<DataTunnelHub> hubContext)
                : base(logger, idService, dateTimeService, hubContext) {
            }

            public ConcurrentDictionary<string, NamedRequest> PublicMorozovRequests {
                get { return requests; }
            }
        }


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        TestableDataTransferService testable;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        Mock<ILogger> loggerMock = new();
        Mock<IIdService> idServiceMock = new();
        Mock<IDateTimeService> dateTimeServiceMock = new();
        Mock<IHubContext<DataTunnelHub>> hubContextMock = new();
        Mock<IClientProxy> clientProxyMock = new();

        DateTime nowDateTime;
        string requestId = string.Empty;

        [SetUp]
        public void Setup() {
            testable = new TestableDataTransferService(loggerMock.Object, idServiceMock.Object, dateTimeServiceMock.Object, hubContextMock.Object);

            var hubClientsMock = new Mock<IHubClients>();
            hubClientsMock
                .SetupGet(x => x.All)
                .Returns(() => {
                    return clientProxyMock.Object;
                });
            hubContextMock
                .SetupGet(x => x.Clients)
                .Returns(() => {
                    return hubClientsMock.Object;
                });

            requestId = "requestId";
            idServiceMock
                .SetupGet(x => x.TransferRequest)
                .Returns(() => {
                    return requestId;
                });

            nowDateTime = DateTime.Now;
            dateTimeServiceMock
                .SetupGet(x => x.Now)
                .Returns(() => {
                    return nowDateTime;
                });
        }

        [Test]
        public async Task RemoveExpiredRequests_Test() {
            DataResponseModel? responseFromCallback = null;
            TransferRequestModel? sendedTransferRequest = null;
            clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, args, cancellationToken) => {
                    sendedTransferRequest = args[0] as TransferRequestModel;
                });

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
#pragma warning disable CS8604 // Possible null reference argument.
            testable.ReceiveResponse(new TransferResponseModel(sendedTransferRequest, new UdpDataResponseModel() { Data = new byte[] { 1, 2, 3, 4 } }));
#pragma warning restore CS8604 // Possible null reference argument.

            Assert.That(responseFromCallback, Is.Not.Null);
            Assert.That(responseFromCallback?.Data, Is.EquivalentTo(new byte[] { 1, 2, 3, 4 }));
        }


        [Test]
        public async Task ReceiveResponse_Is_Single_Action_Test() {
            DataResponseModel? responseFromCallback = null;
            TransferRequestModel? sendedTransferRequest = null;
            clientProxyMock
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((method, args, cancellationToken) => {
                    sendedTransferRequest = args[0] as TransferRequestModel;
                });

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
        }
    }
}
