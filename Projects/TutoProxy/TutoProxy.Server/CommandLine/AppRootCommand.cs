using System.CommandLine;
using System.Net;
using System.Reflection;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
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

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public Argument<string> HostArg { get; }
        public Option<PortsArgument?> TcpOption { get; }
        public Option<PortsArgument?> UdpOption { get; }
        public Option<AllowedClientsOption?> ClientsOption { get; }
        public Option<bool> DaemonOption { get; }

        public AppRootCommand() : base("Connback proxy server TuTo") {
            HostArg = new Argument<string>("host") { Description = "Local host address" };
            TcpOption = PortsArgument.CreateOption("--tcp", $"Allowed ports, format like '--tcp=80,81,443,8000-8100'");
            UdpOption = PortsArgument.CreateOption("--udp", $"Allowed ports, format like '--udp=700-900,65500'");
            ClientsOption = AllowedClientsOption.Create("--clients", $"Allowed Clients IDs, format like '--clients=Client1,Client2'");
            DaemonOption = new Option<bool>("--daemon") { Description = "Run as a daemon", DefaultValueFactory = _ => false };

            Add(HostArg);
            Add(TcpOption);
            Add(UdpOption);
            Add(ClientsOption);
            Add(DaemonOption);

            Validators.Add((result) => {
                try {
                    if(!result.Children.Any(x => x.GetValue(TcpOption) != null || x.GetValue(UdpOption) != null)) {
                        result.AddError("tcp or udp options requried");
                    }
                } catch(InvalidOperationException) {
                    result.AddError("not valid");
                }
            });
        }

        public void ConfigureAction(Serilog.ILogger logger) {
            SetAction(async (parseResult, cancellationToken) => {
                var host = parseResult.GetValue(HostArg);
                var tcp = parseResult.GetValue(TcpOption);
                var udp = parseResult.GetValue(UdpOption);
                var clients = parseResult.GetValue(ClientsOption);
                var daemon = parseResult.GetValue(DaemonOption);

                Guard.NotNullOrEmpty(host, nameof(host));
                Guard.NotNull(tcp ?? udp, "tcp ?? udp");

                var title = $"Connback proxy server TuTo [{host}], TCP ports: {tcp}, UDP-ports: {udp}{(clients != null ? ", clients: " + clients : "")}";
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
                          options.MaximumParallelInvocationsPerClient = 512;
                      })
                    .AddMessagePackProtocol(options => {
                        options.SerializerOptions = MessagePackSerializerOptions.Standard
                            .WithResolver(StandardResolver.Instance)
                            .WithSecurity(MessagePackSecurity.UntrustedData);
                    });

                builder.Services.AddSingleton<IDataTransferService, DataTransferService>();
                builder.Services.AddSingleton<IProcessMonitor, ProcessMonitor>();
                builder.Services.AddSingleton<IServerFactory, ServerFactory>();
                builder.Services.AddSingleton((sp) => logger);
                builder.Services.AddSingleton<IHubClientsService>((sp) => new HubClientsService(
                    sp.GetRequiredService<Serilog.ILogger>(),
                    sp.GetRequiredService<IHostApplicationLifetime>(),
                    sp.GetRequiredService<IServiceProvider>(),
                    sp.GetRequiredService<IProcessMonitor>(),
                    new IPEndPoint(IpAddressHelpers.ParseUrl(host!), 0),
                    tcp?.Ports,
                    udp?.Ports,
                    clients?.Clients)
                );

                var app = builder.Build();
                app.MapHub<SignalRHub>(SignalRParams.Path);

                if(daemon) {
                    Program.ConsoleLevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Warning;
                    TcpSocketParams.TrafficMonitoring = false;
                    UdpSocketParams.TrafficMonitoring = false;
                    await app.RunAsync(host);
                } else {
                    Application.IsMouseDisabled = true;
                    Application.Init();
                    var mainWindow = new MainWindow(title, tcp?.Ports, udp?.Ports);
                    mainWindow.Ready += () => {
                        _ = Task.Run(async () => {
                            await app.RunAsync(host);
                        });
                    };

                    Application.Top.Add(new MainMenu(version), mainWindow);
                    Application.Run();
                    Application.Shutdown();
                }

                return 0;
            });
        }
    }
}
