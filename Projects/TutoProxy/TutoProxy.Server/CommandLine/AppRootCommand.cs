﻿using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Reflection;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Terminal.Gui;
using TutoProxy.Server.Communication;
using TutoProxy.Server.Hubs;
using TutoProxy.Server.Services;
using TutoProxy.Server.Windows;
using TuToProxy.Core;
using TuToProxy.Core.CommandLine;
using TuToProxy.Core.Helpers;
using TuToProxy.Core.ServiceProvider;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Connback proxy server TuTo") {
            Add(new Argument<string>("host", "Local host address"));
            var tcpOption = PortsArgument.CreateOption("--tcp", $"Allowed ports, format like '--tcp=80,81,443,8000-8100'");
            var udpOption = PortsArgument.CreateOption("--udp", $"Allowed ports, format like '--udp=700-900,65500'");
            Add(tcpOption);
            Add(udpOption);
            Add(AllowedClientsOption.Create("--clients", $"Allowed Clients IDs, format like '--clients=Client1,Client2'"));

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
            readonly Serilog.ILogger logger;
            readonly IHostApplicationLifetime applicationLifetime;

            public string? Host { get; set; }
            public PortsArgument? Udp { get; set; }
            public PortsArgument? Tcp { get; set; }
            public AllowedClientsOption? Clients { get; set; }

            public Handler(
                Serilog.ILogger logger,
                IHostApplicationLifetime applicationLifetime
                ) {
                Guard.NotNull(logger, nameof(logger));
                Guard.NotNull(applicationLifetime, nameof(applicationLifetime));
                this.logger = logger;
                this.applicationLifetime = applicationLifetime;
            }

            public Task<int> InvokeAsync(InvocationContext context) {
                Guard.NotNullOrEmpty(Host, nameof(Host));
                Guard.NotNull(Tcp ?? Udp, "Tcp ?? Udp");

                var title = $"Connback proxy server TuTo [{Host}], TCP ports: {Tcp}, UDP-ports: {Udp}{(Clients != null ? ", clients: " + Clients : "")}";
                var version = $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";
                logger.Information(version);
                logger.Information(title);

                var builder = WebApplication.CreateBuilder();

                builder.Host.UseServiceProviderFactory(context => {
                    var factory = ServiceProviderFactory.Instance;
                    return ServiceProviderFactory.Instance;
                });

                builder.Logging.ClearProviders();
                builder.Logging.AddSerilog();

                builder.Services.
                    AddSignalR()
                      .AddHubOptions<SignalRHub>(options => {
                          options.MaximumReceiveMessageSize = 512 * 1024;
                          options.MaximumParallelInvocationsPerClient = 256;
                          //options.EnableDetailedErrors = true;
                      })
                    .AddMessagePackProtocol(options => {
                        StaticCompositeResolver.Instance.Register(
                            MessagePack.Resolvers.StandardResolver.Instance
                        );
                        options.SerializerOptions = MessagePackSerializerOptions.Standard
                            .WithResolver(StaticCompositeResolver.Instance)
                            .WithSecurity(MessagePackSecurity.UntrustedData);
                    });

                builder.Services.AddSingleton<IIdService, IdService>();
                builder.Services.AddSingleton<IDateTimeService, DateTimeService>();
                builder.Services.AddSingleton<IDataTransferService, DataTransferService>();
                builder.Services.AddSingleton<IProcessMonitor, ProcessMonitor>();
                builder.Services.AddSingleton<IServerFactory, ServerFactory>();
                builder.Services.AddSingleton<IHubClientsService>((sp) => new HubClientsService(
                    sp.GetRequiredService<Serilog.ILogger>(),
                    sp.GetRequiredService<IHostApplicationLifetime>(),
                    sp.GetRequiredService<IServiceProvider>(),
                    sp.GetRequiredService<IProcessMonitor>(),
                    new IPEndPoint(IpAddressHelpers.ParseUrl(Host!), 0),
                    Tcp?.Ports,
                    Udp?.Ports,
                    Clients?.Clients)
                );

                var app = builder.Build();
                app.MapHub<SignalRHub>(SignalRParams.Path);

                Application.IsMouseDisabled = true;
                Application.Init();

                var mainWindow = new MainWindow(title, Tcp?.Ports, Udp?.Ports);
                mainWindow.Ready += () => {
                    _ = Task.Run(async () => {
                        await app.RunAsync(Host);
                    });
                };

                Application.Top.Add(new MainMenu(version), mainWindow);
                Application.Run();
                Application.Shutdown();

                return Task.FromResult(0);
            }
        }
    }
}
