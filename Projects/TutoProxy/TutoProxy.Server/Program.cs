using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Hosting;
using Serilog.Core;
using Serilog.Events;
using TutoProxy.Server.CommandLine;
using TuToProxy.Core.ServiceProvider;

class Program {
    public static readonly LoggingLevelSwitch ConsoleLevelSwitch
        = new LoggingLevelSwitch(LogEventLevel.Fatal);

    public static async Task<int> Main(string[] args) {
        var runner = new CommandLineBuilder(new AppRootCommand())
            .UseHost(_ => new HostBuilder(), builder => builder
                .UseCommandHandler<AppRootCommand, AppRootCommand.Handler>()
                .UseSerilog((_, config) => config
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u1}]{Message:lj}{NewLine}{Exception}", levelSwitch: ConsoleLevelSwitch)
                    .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day,
                             restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
                )
                .UseServiceProviderFactory(context => {
                    var factory = ServiceProviderFactory.Instance;
                    return ServiceProviderFactory.Instance;
                })
            )
            .UseDefaults()
            .Build();

        return await runner.InvokeAsync(args);
    }

}