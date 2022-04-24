using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TutoProxy.Server.CommandLine;
using TutoProxy.Server.Services;
using TuToProxy.Core.ServiceProvider;

class Program {
    public static async Task<int> Main(string[] args) {
        var runner = new CommandLineBuilder(new AppRootCommand())
            .UseHost(_ => new HostBuilder(), builder => builder
                .UseCommandHandler<AppRootCommand, AppRootCommand.Handler>()
                .UseSerilog((_, config) => config
                    .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}{Exception}")
                    .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day)
                )
                .UseServiceProviderFactory(context => {
                    var factory = ServiceProviderFactory.Instance;
                    return ServiceProviderFactory.Instance;
                })
                .ConfigureServices((h, services) => {
                    services.AddSingleton<IDataTransferService, DataTransferService>();
                })
            )
            .UseDefaults()
            .Build();

        return await runner.InvokeAsync(args);
    }

}