using System.CommandLine;
using System.Net;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Terminal.Gui;
using TutoProxy.Client.Communication;
using TutoProxy.Client.Services;
using TutoProxy.Client.Windows;
using TuToProxy.Core;
using TuToProxy.Core.CommandLine;

namespace TutoProxy.Server.CommandLine {
    public class AppRootCommand : RootCommand {
        public Argument<string> ServerArg { get; }
        public Argument<string> SendtoArg { get; }
        public Argument<string> IdArg { get; }
        public Option<PortsArgument?> TcpOption { get; }
        public Option<PortsArgument?> UdpOption { get; }
        public Option<TransportProtocol> ProtocolOption { get; }
        public Option<int> ParallelOption { get; }
        public Option<bool> DaemonOption { get; }

        public AppRootCommand() : base("Connback proxy client TuTo") {
            ServerArg = new Argument<string>("server") { Description = "Remote server address" };
            SendtoArg = new Argument<string>("sendto") { Description = "Sendto IP address" };
            IdArg = new Argument<string>("--id") { Description = "Client ID" };
            TcpOption = PortsArgument.CreateOption("--tcp", $"Tunneling ports, format like '--tcp=80,81,443,8000-8100'");
            UdpOption = PortsArgument.CreateOption("--udp", $"Tunneling ports, format like '--udp=700-900,65500'");
            ProtocolOption = new Option<TransportProtocol>("--protocol") { Description = "Transport protocol: Auto, Http, WebSocket", DefaultValueFactory = _ => TransportProtocol.Auto };
            ParallelOption = new Option<int>("--parallel") { Description = "Number of parallel SignalR connections (1-8)", DefaultValueFactory = _ => 1 };
            DaemonOption = new Option<bool>("--daemon") { Description = "Run as a daemon", DefaultValueFactory = _ => false };

            Add(ServerArg);
            Add(SendtoArg);
            Add(IdArg);
            Add(TcpOption);
            Add(UdpOption);
            Add(ProtocolOption);
            Add(ParallelOption);
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

        public void ConfigureAction(
            Serilog.ILogger logger,
            ISignalRClient signalrClient,
            IHostApplicationLifetime applicationLifetime,
            IClientsService clientsService) {
            SetAction((parseResult, cancellationToken) => {
                var server = parseResult.GetValue(ServerArg);
                var sendto = parseResult.GetValue(SendtoArg);
                var id = parseResult.GetValue(IdArg);
                var tcp = parseResult.GetValue(TcpOption);
                var udp = parseResult.GetValue(UdpOption);
                var protocol = parseResult.GetValue(ProtocolOption);
                var parallel = parseResult.GetValue(ParallelOption);
                var daemon = parseResult.GetValue(DaemonOption);

                Guard.NotNull(server, nameof(server));
                Guard.NotNullOrEmpty(sendto, nameof(sendto));
                Guard.NotNull(id, nameof(id));
                Guard.NotNull(tcp ?? udp, $"tcp ?? udp");

                var title = $"Connback proxy client TuTo [{id}], {server} >>>> {sendto}";
                var version = $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";
                logger.Information(version);
                logger.Information(title);

                using var appStoppingReg = applicationLifetime.ApplicationStopping.Register(async () => {
                    await signalrClient.StopAsync();
                    await clientsService.StopAsync();
                });

                if(daemon) {
                    Program.ConsoleLevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Warning;
                    TcpSocketParams.TrafficMonitoring = false;
                    UdpSocketParams.TrafficMonitoring = false;
                    _ = StartServices(server!, sendto!, id!, tcp, udp, protocol, parallel, logger, signalrClient, clientsService, appStoppingReg.Token, (status) => logger.Information($"server: {status}"));
                    _ = appStoppingReg.Token.WaitHandle.WaitOne();
                } else {
                    Application.IsMouseDisabled = true;
                    Application.Init();
                    var mainWindow = new MainWindow(title, tcp?.Ports, udp?.Ports);
                    mainWindow.Ready += () => {
                        _ = StartServices(server!, sendto!, id!, tcp, udp, protocol, parallel, logger, signalrClient, clientsService, appStoppingReg.Token, (status) => Application.MainLoop.Invoke(() => { mainWindow.Title = $"{title} - {status}"; }));
                    };

                    Application.Top.Add(new MainMenu(version), mainWindow);
                    Application.Run();
                    Application.Shutdown();
                    applicationLifetime.StopApplication();
                }

                return Task.FromResult(0);
            });
        }

        static async Task StartServices(
            string server, string sendto, string id,
            PortsArgument? tcp, PortsArgument? udp,
            TransportProtocol protocol, int parallel,
            Serilog.ILogger logger,
            ISignalRClient signalrClient,
            IClientsService clientsService,
            CancellationToken cancellationToken,
            Action<string> logStatus) {
            try {
                await clientsService.StartAsync(IPAddress.Parse(sendto), tcp?.Ports, udp?.Ports);
                _ = Task.Run(async () => {
                    while(!cancellationToken.IsCancellationRequested) {
                        try {
                            logStatus("connection to server...");
                            var connectionId = await signalrClient.StartAsync(server, tcp?.Argument, udp?.Argument, id, protocol, Math.Clamp(parallel, 1, 8), cancellationToken);

                            logStatus($"{connectionId}");
                            break;
                        } catch(HttpRequestException) {
                            logger.Error("Connection failed");
                            logStatus("connection failed. Retry...");
                            await Task.Delay(5000, cancellationToken);
                            logger.Information("Retry connect");
                            continue;
                        }
                    }
                }, cancellationToken);
            } catch(Exception ex) {
                logger.Error($"StartServices error: {ex.Message}");
            }
        }
    }
}
