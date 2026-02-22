using Serilog.Core;
using Serilog.Events;
using TutoProxy.Server.CommandLine;

class Program {
    public static readonly LoggingLevelSwitch ConsoleLevelSwitch
        = new LoggingLevelSwitch(LogEventLevel.Fatal);

    public static async Task<int> Main(string[] args) {
        var logger = new Serilog.LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u1}]{Message:lj}{NewLine}{Exception}", levelSwitch: ConsoleLevelSwitch)
            .WriteTo.File("log-.txt", rollingInterval: Serilog.RollingInterval.Day,
                     restrictedToMinimumLevel: LogEventLevel.Warning)
            .CreateLogger();

        Serilog.Log.Logger = logger;

        var command = new AppRootCommand();
        command.ConfigureAction(logger);

        var parseResult = command.Parse(args);
        return await parseResult.InvokeAsync();
    }
}
