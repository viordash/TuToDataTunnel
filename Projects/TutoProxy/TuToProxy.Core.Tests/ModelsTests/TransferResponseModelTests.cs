using System.Text.Json;
using NUnit.Framework;
using TutoProxy.Core.Models;

namespace TuToProxy.Core.Tests.ModelsTests {
    public class TransferResponseModelTests {

        [Test]
        public void Serializable_Test() {
            var testable = new TransferResponseModel();
            string jsonString = JsonSerializer.Serialize(testable);
            Assert.That(jsonString, Is.Not.Empty);
        }
    }
}
