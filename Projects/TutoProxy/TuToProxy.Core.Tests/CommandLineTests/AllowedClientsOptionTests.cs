using System;
using NUnit.Framework;
using TuToProxy.Core.CommandLine;

namespace TuToProxy.Core.Tests.CommandLineTests {
    public class AllowedClientsOptionTests {
        [SetUp]
        public void Setup() {
        }

        [Test]
        public void ToParseArgument_Empty_Value_Throws_ArgumentException() {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.Throws<ArgumentException>(() => AllowedClientsOption.Parse(null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.Throws<ArgumentException>(() => AllowedClientsOption.Parse(""));
            Assert.Throws<ArgumentException>(() => AllowedClientsOption.Parse(string.Empty));
        }

        [Test]
        public void ToParseArgument_Test() {
            Assert.That(AllowedClientsOption.Parse("Client0").Clients, Is.EquivalentTo(new string[] { "Client0" }));
            Assert.That(AllowedClientsOption.Parse("Client1,65535").Clients, Is.EquivalentTo(new string[] { "Client1", "65535" }));
            Assert.That(AllowedClientsOption.Parse("Client0,Client1,Client3").Clients, Is.EquivalentTo(new string[] { "Client0", "Client1", "Client3" }));
        }
    }
}