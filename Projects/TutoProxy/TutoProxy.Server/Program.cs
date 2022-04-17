using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Hosting;
using TutoProxy.Server.CommandLine;

class Program {
    public static async Task<int> Main(string[] args) {
        var runner = new CommandLineBuilder(new AppRootCommand())
            .UseHost(_ => new HostBuilder(), (builder) => builder
                .ConfigureServices((_, services) => {

                })
                .UseCommandHandler<AppRootCommand, AppRootCommand.Handler>()
             )
            .UseDefaults()
            .Build();
        return await runner.InvokeAsync(args);
    }
}