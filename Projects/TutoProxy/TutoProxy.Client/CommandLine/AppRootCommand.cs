using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;
using TutoProxy.Core.CommandLine;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Прокси клиент TuTo") {
            Add(new Argument<string>("server", "Remote server address"));
            var tcpOption = PortsArgument.CreateOption("--tcp", $"Tunneling ports, format like '--tcp=80,81,443,8000-8100'");
            var udpOption = PortsArgument.CreateOption("--udp", $"Tunneling ports, format like '--udp=700-900,65500'");
            Add(tcpOption);
            Add(udpOption);
            AddValidator((result) => {
                try {
                    if(!result.Children.Any(x => x.GetValueForOption(tcpOption) != null || x.GetValueForOption(udpOption) != null)) {
                        result.ErrorMessage = "tcp or udp options requried";
                    }
                } catch(InvalidOperationException) {
                    result.ErrorMessage = "not valid";
                }
            });
        }

        public new class Handler : ICommandHandler {
            readonly ILogger logger;
            readonly ISignalRClient signalrClient;
            readonly IHostApplicationLifetime applicationLifetime;
            readonly IClientsService clientsService;

            public string? Server { get; set; }
            public PortsArgument? Udp { get; set; }
            public PortsArgument? Tcp { get; set; }

            public Handler(
                ILogger logger,
                ISignalRClient signalrClient,
                IHostApplicationLifetime applicationLifetime,
                IClientsService clientsService
                ) {
                Guard.NotNull(logger, nameof(logger));
                Guard.NotNull(signalrClient, nameof(signalrClient));
                Guard.NotNull(applicationLifetime, nameof(applicationLifetime));
                Guard.NotNull(clientsService, nameof(clientsService));
                this.logger = logger;
                this.signalrClient = signalrClient;
                this.applicationLifetime = applicationLifetime;
                this.clientsService = clientsService;
            }

            public async Task<int> InvokeAsync(InvocationContext context) {
                Guard.NotNull(Server, nameof(Server));
                Guard.NotNull(Tcp ?? Udp, $"Tcp ?? Udp");

                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"Прокси клиент TuTo, сервер {Server}");

                using var appStoppingReg = applicationLifetime.ApplicationStopping.Register(async () => {
                    await signalrClient.StopAsync();
                    clientsService.Stop();
                });

                while(!appStoppingReg.Token.IsCancellationRequested) {
                    try {
                        await signalrClient.StartAsync(Server!, Tcp?.Argument, Udp?.Argument, appStoppingReg.Token);
                        break;
                    } catch(HttpRequestException) {
                        logger.Error("Connection failed");
                        await Task.Delay(5000, appStoppingReg.Token);
                        logger.Information("Retry connect");
                        continue;
                    }
                }

                clientsService.Start(Tcp?.Ports, Udp?.Ports);
                _ = appStoppingReg.Token.WaitHandle.WaitOne();
                return 0;
            }
        }
    }
}
