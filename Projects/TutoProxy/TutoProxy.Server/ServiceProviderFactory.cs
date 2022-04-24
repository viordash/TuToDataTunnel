using Microsoft.Extensions.DependencyInjection;

namespace TutoProxy.Server {
    public class ServiceProviderFactory : IServiceProviderFactory<IServiceCollection> {
        static ServiceProviderFactory? instance;
        public static IServiceProviderFactory<IServiceCollection> Instance {
            get {
                if(instance == null) {
                    instance = new ServiceProviderFactory();
                }
                return instance;
            }
        }

        static IServiceCollection serviceCollection = new ServiceCollection();

        public IServiceCollection CreateBuilder(IServiceCollection services) {
            foreach(var item in services) {
                if(serviceCollection.Contains(item)) {
                    serviceCollection.Remove(item);
                }
                serviceCollection.Add(item);
            }
            return serviceCollection;
        }

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder) {
            return serviceCollection.BuildServiceProvider();
        }
    }
}
