using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Terminal.Gui;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;
using TutoProxy.Client.Windows;
using TuToProxy.Core.CommandLine;
using TuToProxy.Core.Helpers;

namespace TutoProxy.Server.CommandLine {
    public class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Прокси клиент TuTo") {
            Add(new Argument<string>("server", "Remote server address"));
            Add(new Argument<string>("sendto", "Sendto IP address"));
            Add(new Option<string>("--id", "Client ID"));
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
            readonly IProcessMonitor processMonitor;

            public string? Server { get; set; }
            public string? Sendto { get; set; }
            public string? Id { get; set; }
            public PortsArgument? Udp { get; set; }
            public PortsArgument? Tcp { get; set; }

            public Handler(
                ILogger logger,
                ISignalRClient signalrClient,
                IHostApplicationLifetime applicationLifetime,
                IClientsService clientsService,
                IProcessMonitor processMonitor
                ) {
                Guard.NotNull(logger, nameof(logger));
                Guard.NotNull(signalrClient, nameof(signalrClient));
                Guard.NotNull(applicationLifetime, nameof(applicationLifetime));
                Guard.NotNull(clientsService, nameof(clientsService));
                Guard.NotNull(processMonitor, nameof(processMonitor));
                this.logger = logger;
                this.signalrClient = signalrClient;
                this.applicationLifetime = applicationLifetime;
                this.clientsService = clientsService;
                this.processMonitor = processMonitor;
            }

            public Task<int> InvokeAsync(InvocationContext context) {
                Guard.NotNull(Server, nameof(Server));
                Guard.NotNullOrEmpty(Sendto, nameof(Sendto));
                Guard.NotNull(Tcp ?? Udp, $"Tcp ?? Udp");

                var title = $"Connback proxy client TuTo [{Id}], {Server} >>>> {Sendto}";
                var version = $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";
                logger.Information(version);
                logger.Information(title);

                using var appStoppingReg = applicationLifetime.ApplicationStopping.Register(async () => {
                    await signalrClient.StopAsync();
                    clientsService.Stop();
                });

                Application.Init();

                var mainWindow = new MainWindow(title);
                mainWindow.Ready += () => {
                    clientsService.Start(IPAddress.Parse(Sendto!), Tcp?.Ports, Udp?.Ports);
                    _ = Task.Run(async () => {
                        while(!appStoppingReg.Token.IsCancellationRequested) {
                            try {
                                Application.MainLoop.Invoke(() => {
                                    mainWindow.Title = $"{title} - connection to server...";
                                });
                                await signalrClient.StartAsync(Server!, Tcp?.Argument, Udp?.Argument, Id, appStoppingReg.Token);

                                Application.MainLoop.Invoke(() => {
                                    mainWindow.Title = $"{title} - connected";
                                });
                                break;
                            } catch(HttpRequestException) {
                                logger.Error("Connection failed");
                                Application.MainLoop.Invoke(() => {
                                    mainWindow.Title = $"{title} - connection failed. Retry...";
                                });
                                await Task.Delay(5000, appStoppingReg.Token);
                                logger.Information("Retry connect");
                                continue;
                            }
                        }
                    }, appStoppingReg.Token);
                };

                Application.Top.Add(new MainMenu(version), mainWindow);
                Application.Run();

                Application.Shutdown();
                applicationLifetime.StopApplication();

                return Task.FromResult(0);
            }
        }
    }
}
