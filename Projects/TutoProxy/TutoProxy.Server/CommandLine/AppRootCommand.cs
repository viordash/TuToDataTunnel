using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TutoProxy.Core.CommandLine;
using TutoProxy.Server.Hubs;
using TutoProxy.Server.Services;
using TuToProxy.Core;
using TuToProxy.Core.Helpers;
using TuToProxy.Core.ServiceProvider;
using TuToProxy.Core.Services;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Прокси сервер TuTo") {
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
            readonly ILogger logger;
            readonly IHostApplicationLifetime applicationLifetime;

            public string? Host { get; set; }
            public PortsArgument? Udp { get; set; }
            public PortsArgument? Tcp { get; set; }
            public AllowedClientsOption? Clients { get; set; }

            public Handler(
                ILogger logger,
                IHostApplicationLifetime applicationLifetime
                ) {
                Guard.NotNull(logger, nameof(logger));
                Guard.NotNull(applicationLifetime, nameof(applicationLifetime));
                this.logger = logger;
                this.applicationLifetime = applicationLifetime;
            }

            public async Task<int> InvokeAsync(InvocationContext context) {
                Guard.NotNullOrEmpty(Host, nameof(Host));
                Guard.NotNull(Tcp ?? Udp, "Tcp ?? Udp");

                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"Прокси сервер TuTo, хост {Host}, доступные tcp-порты {Tcp}, udp-порты {Udp}{(Clients != null ? ", клиенты " + Clients : "")}");


                var builder = WebApplication.CreateBuilder();

                builder.Host.UseServiceProviderFactory(context => {
                    var factory = ServiceProviderFactory.Instance;
                    return ServiceProviderFactory.Instance;
                });

                builder.Services.AddSignalR();
                builder.Services.AddSingleton<IIdService, IdService>();
                builder.Services.AddSingleton<IDateTimeService, DateTimeService>();
                builder.Services.AddSingleton<IDataTransferService, DataTransferService>();
                builder.Services.AddSingleton<IHubClientsService>((sp) => new HubClientsService(
                    sp.GetRequiredService<ILogger>(),
                    sp.GetRequiredService<IHostApplicationLifetime>(),
                    sp.GetRequiredService<IServiceProvider>(),
                    new IPEndPoint(IpAddressHelpers.ParseUrl(Host!), 0),
                    Tcp?.Ports,
                    Udp?.Ports,
                    Clients?.Clients)
                );

                var app = builder.Build();
                app.MapHub<SignalRHub>(SignalRParams.Path);

                await app.RunAsync(Host);
                return 0;
            }
        }
    }
}
