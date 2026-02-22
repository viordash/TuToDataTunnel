using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TutoProxy.Server.CommandLine;

class Program {
    public static async Task<int> Main(string[] args) {
        using var host = Host.CreateDefaultBuilder(args)
            .UseSerilog((_, config) => config
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u1}]{Message:lj}{NewLine}{Exception}")
                .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day)
            )
            .Build();

        await host.StartAsync();

        var logger = host.Services.GetRequiredService<Serilog.ILogger>();
        var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        var command = new AppRootCommand();
        command.ConfigureAction(logger, applicationLifetime.ApplicationStopping);

        var parseResult = command.Parse(args);
        var result = await parseResult.InvokeAsync();

        await host.StopAsync();
        return result;
    }
}
