using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using TutoProxy.Client.Communication;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Прокси клиент TuTo") {
            Add(new Argument<string>("server", "Remote server address"));
        }

        public new class Handler : ICommandHandler {
            readonly ILogger logger;
            readonly IDataTunnelClient dataTunnelClient;
            readonly IHostApplicationLifetime applicationLifetime;

            public string? Server { get; set; }

            public Handler(
                ILogger logger,
                IDataTunnelClient dataTunnelClient,
                IHostApplicationLifetime applicationLifetime
                ) {
                Guard.NotNull(logger, nameof(logger));
                Guard.NotNull(dataTunnelClient, nameof(dataTunnelClient));
                Guard.NotNull(applicationLifetime, nameof(applicationLifetime));
                this.logger = logger;
                this.dataTunnelClient = dataTunnelClient;
                this.applicationLifetime = applicationLifetime;
            }

            public async Task<int> InvokeAsync(InvocationContext context) {
                if(string.IsNullOrEmpty(Server)) {
                    return -1;
                }

                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"Прокси клиент TuTo, сервер {Server}");

                using var appStoppingReg = applicationLifetime.ApplicationStopping.Register(async () => {
                    await dataTunnelClient.StopAsync(default);
                });

                while(!appStoppingReg.Token.IsCancellationRequested) {
                    try {
                        await dataTunnelClient.StartAsync(Server, appStoppingReg.Token);
                        break;
                    } catch(HttpRequestException) {
                        logger.Error("Connection failed");
                        await Task.Delay(5000, appStoppingReg.Token);
                        logger.Information("Retry connect");
                        continue;
                    }
                }

                appStoppingReg.Token.WaitHandle.WaitOne();
                return 0;
            }
        }
    }
}
