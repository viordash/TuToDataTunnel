using System.Text.Json;
using NUnit.Framework;
using TutoProxy.Core.Models;

namespace TuToProxy.Core.Tests.ModelsTests {
    public class TransferResponseModelTests {

        [Test]
        public void UdpSerializable_Test() {
            var testable = new TransferUdpResponseModel();
            string jsonString = JsonSerializer.Serialize(testable);
            Assert.That(jsonString, Is.Not.Empty);
        }

        [Test]
        public void TcpSerializable_Test() {
            var testable = new TransferTcpResponseModel();
            string jsonString = JsonSerializer.Serialize(testable);
            Assert.That(jsonString, Is.Not.Empty);
        }
    }
}
