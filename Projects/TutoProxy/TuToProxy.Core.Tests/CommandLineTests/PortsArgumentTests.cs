using System;
using NUnit.Framework;
using TuToProxy.Core.CommandLine;

namespace TuToProxy.Core.Tests.CommandLineTests {
    public class PortsArgumentTests {
        [SetUp]
        public void Setup() {
        }

        [Test]
        public void ToParseArgument_Empty_Value_Throws_ArgumentException() {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse(null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse(""));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse(string.Empty));
        }

        [Test]
        public void ToParseArgument_Incorrect_Value_Throws_ArgumentException() {
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("test"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("80, 81"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("80 ,81"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse(","));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("0,81"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("65536,81"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse(" 81"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("81 "));
        }

        [Test]
        public void ToParseArgument_Incorrect_Ranges_Throws_ArgumentException() {
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("100-99,81"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("99- 100"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("99 -100"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse(" 99-100"));
            Assert.Throws<ArgumentException>(() => PortsArgument.Parse("99-100 "));
        }

        [Test]
        public void ToParseArgument_Test() {
            Assert.That(PortsArgument.Parse("80").Ports, Is.EquivalentTo(new int[] { 80 }));
            Assert.That(PortsArgument.Parse("1,65535").Ports, Is.EquivalentTo(new int[] { 1, 65535 }));
            Assert.That(PortsArgument.Parse("80,81,443").Ports, Is.EquivalentTo(new int[] { 80, 81, 443 }));
            Assert.That(PortsArgument.Parse("80,700-705,100-103").Ports, Is.EquivalentTo(new int[] { 80, 700, 701, 702, 703, 704, 705, 100, 101, 102, 103 }));
        }
    }
}