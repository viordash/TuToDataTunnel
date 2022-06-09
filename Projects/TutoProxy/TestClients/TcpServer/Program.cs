using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Hosting;
using TutoProxy.Server.CommandLine;

class Program {
    public static async Task<int> Main(string[] args) {
        var runner = new CommandLineBuilder(new AppRootCommand())
            .UseHost(_ => new HostBuilder(), builder => builder
                .UseCommandHandler<AppRootCommand, AppRootCommand.Handler>()
                .UseSerilog((_, config) => config
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u1}]{Message:lj}{NewLine}{Exception}")
                    .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day)
                )
            )
            .UseDefaults()
            .Build();
        return await runner.InvokeAsync(args);
    }
}