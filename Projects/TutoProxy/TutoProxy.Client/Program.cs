using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Core;
using Serilog.Events;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;
using TutoProxy.Server.CommandLine;
using TuToProxy.Core.ServiceProvider;

class Program {
    public static readonly LoggingLevelSwitch ConsoleLevelSwitch
        = new LoggingLevelSwitch(LogEventLevel.Fatal);

    public static async Task<int> Main(string[] args) {
        using var host = Host.CreateDefaultBuilder(args)
            .UseSerilog((_, config) => config
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u1}]{Message:lj}{NewLine}{Exception}", levelSwitch: ConsoleLevelSwitch)
                .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day,
                         restrictedToMinimumLevel: LogEventLevel.Warning)
            )
            .UseServiceProviderFactory(context => {
                var factory = ServiceProviderFactory.Instance;
                return ServiceProviderFactory.Instance;
            })
            .ConfigureServices((_, services) => {
                services.AddSingleton<ISignalRClient, SignalRClient>();
                services.AddSingleton<IClientsService, ClientsService>();
                services.AddSingleton<IClientFactory, ClientFactory>();
                services.AddSingleton<IProcessMonitor, ProcessMonitor>();
            })
            .Build();

        await host.StartAsync();

        var logger = host.Services.GetRequiredService<Serilog.ILogger>();
        var signalrClient = host.Services.GetRequiredService<ISignalRClient>();
        var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var clientsService = host.Services.GetRequiredService<IClientsService>();

        var command = new AppRootCommand();
        command.ConfigureAction(logger, signalrClient, applicationLifetime, clientsService);

        var parseResult = command.Parse(args);
        var result = await parseResult.InvokeAsync();

        await host.StopAsync();
        return result;
    }
}
