using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Serilog;
using TutoProxy.Core.Models;
using TutoProxy.Server.Services;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.Tests.Services {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public class RequestProcessingServiceTests {
        RequestProcessingService testable;

        Mock<IDataTransferService> dataTransferServiceMock;

        TimeSpan timeout;

        [SetUp]
        public void Setup() {
            var loggerMock = new Mock<ILogger>();
            var dateTimeServiceMock = new Mock<IDateTimeService>();
            dataTransferServiceMock = new();

            testable = new RequestProcessingService(loggerMock.Object, dataTransferServiceMock.Object, dateTimeServiceMock.Object);

            timeout = TimeSpan.FromMilliseconds(200);
            dateTimeServiceMock
                .SetupGet(x => x.RequestTimeout)
                .Returns(() => timeout);
        }

        [Test]
        public async Task Request_Test() {
            dataTransferServiceMock
                .Setup(x => x.SendRequest(It.Is<DataRequestModel>(r => r.Data.SequenceEqual(new byte[] { 10, 20, 30, 40 })), It.IsAny<Action<DataResponseModel>>()))
                .Callback<DataRequestModel, Action<DataResponseModel>>((request, responseCallback) => {
                    responseCallback(new UdpDataResponseModel() {
                        Data = new byte[] { 1, 2, 3, 4 }
                    });
                });

            var requestModel = new UdpDataRequestModel() {
                Data = new byte[] { 10, 20, 30, 40 }
            };
            var response = await testable.Request(requestModel);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Data, Is.EquivalentTo(new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void Request_Timeout_Throws_TaskCanceledException() {
            timeout = TimeSpan.FromMilliseconds(100);

            var requestModel = new UdpDataRequestModel() {
                Data = new byte[] { 10, 20, 30, 40 }
            };
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            Assert.ThrowsAsync<TaskCanceledException>(() => testable.Request(requestModel));
            stopWatch.Stop();
            Assert.That(stopWatch.Elapsed, Is.GreaterThanOrEqualTo(timeout));
        }
    }
}
