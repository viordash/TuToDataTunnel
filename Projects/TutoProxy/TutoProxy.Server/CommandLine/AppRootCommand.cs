using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TutoProxy.Core.CommandLine;
using TutoProxy.Core.Models;
using TutoProxy.Server.Hubs;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.ServiceProvider;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Прокси сервер TuTo") {
            Add(new Argument<string>("host", "Local host address"));
            var tcpOption = PortsArgument.CreateOption("--tcp");
            var udpOption = PortsArgument.CreateOption("--udp");
            Add(tcpOption);
            Add(udpOption);
            Add(new Option<bool>("--verbose", "Show the verbose logs"));
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

            public string? Host { get; set; }
            public PortsArgument? Udp { get; set; }
            public PortsArgument? Tcp { get; set; }
            public bool Verbose { get; set; }

            public Handler(
                ILogger logger
                ) {
                Guard.NotNull(logger, nameof(logger));
                this.logger = logger;
            }

            public async Task<int> InvokeAsync(InvocationContext context) {
                Guard.NotNullOrEmpty(Host, nameof(Host));
                Guard.NotNull(Tcp, nameof(Tcp));
                Guard.NotNull(Udp, nameof(Udp));

                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"Прокси сервер TuTo, хост {Host}, tcp-порты {Tcp}, udp-порты {Udp}");

                var builder = WebApplication.CreateBuilder();

                builder.Host.UseServiceProviderFactory(context => {
                    var factory = ServiceProviderFactory.Instance;
                    return ServiceProviderFactory.Instance;
                });

                builder.Services.AddSignalR();
                builder.Services.AddSingleton<IDataTransferService, DataTransferService>();
                builder.Services.AddSingleton<IRequestProcessingService, RequestProcessingService>();

                var app = builder.Build();
                app.MapHub<DataTunnelHub>(DataTunnelParams.Path);

                var requestProcessingService = app.Services.GetRequiredService<IRequestProcessingService>();

                _ = Task.Run(async () => {
                    while(true) {
                        await Task.Delay(300);
                        try {
                            var response = await requestProcessingService.Request(new UdpDataRequestModel() {
                                Data = System.Text.Encoding.UTF8.GetBytes($"staaaaart")
                            });
                        } catch(OperationCanceledException) {
                            await Task.Delay(1000);
                        }
                    }
                });

                await app.RunAsync(Host);
                return 0;
            }
        }
    }
}
