using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using TuToProxy.Core.ServiceProvider;

namespace TuToProxy.Core.Tests.ServiceProviderTests {
    public class ServiceProviderFactoryTests {
        [SetUp]
        public void Setup() {
        }

        [Test]
        public void Singleton_Instance_Test() {
            var instance0 = ServiceProviderFactory.Instance;
            var instance1 = ServiceProviderFactory.Instance;
            Assert.That(instance0, Is.SameAs(instance1));
        }

        [Test]
        public void CreateBuilder_Return_Common_Collection_Test() {
            var instance = ServiceProviderFactory.Instance;
            var extServices0 = new ServiceCollection();
            var builder0 = instance.CreateBuilder(extServices0);

            var extServices1 = new ServiceCollection();
            var builder1 = instance.CreateBuilder(extServices1);

            Assert.That(builder0, Is.SameAs(builder1));
        }

        [Test]
        public void CreateBuilder_Accumulate_External_Services() {
            var instance = ServiceProviderFactory.Instance;
            var extServices0 = new ServiceCollection();
            extServices0.AddSingleton(this);
            instance.CreateBuilder(extServices0);

            var extServices1 = new ServiceCollection();
            extServices1.AddSingleton(this);
            var builder = instance.CreateBuilder(extServices1);

            Assert.That(builder.Count, Is.EqualTo(2));
        }

        [Test]
        public void CreateServiceProvider_Is_From_Internal_Collection_Test() {
            var instance = ServiceProviderFactory.Instance;
            var extServices0 = new ServiceCollection();
            extServices0.AddSingleton(this);
            instance.CreateBuilder(extServices0);
            var serviceProvider = instance.CreateServiceProvider(new ServiceCollection());
            Assert.That(serviceProvider.GetService<ServiceProviderFactoryTests>(), Is.SameAs(this));
        }
    }
}
