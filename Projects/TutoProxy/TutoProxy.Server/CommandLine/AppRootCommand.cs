using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TutoProxy.Core.CommandLine;
using TutoProxy.Server.Hubs;
using TutoProxy.Server.Services;
using TuToProxy.Core.ServiceProvider;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Прокси сервер TuTo") {
            Add(new Argument<string>("host", "Local host address"));
            Add(new Argument<string>("test"));
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
            public string? Test { get; set; }
            public PortsArgument? Udp { get; set; }
            public PortsArgument? Tcp { get; set; }
            public bool Verbose { get; set; }

            public Handler(
                ILogger logger) {
                Guard.NotNull(logger, nameof(logger));
                this.logger = logger;
            }

            public async Task<int> InvokeAsync(InvocationContext context) {
                if(string.IsNullOrEmpty(Host) || (Tcp == null && Udp == null)) {
                    return -1;
                }

                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"Прокси сервер TuTo, хост {Host}, tcp-порты {Tcp}, udp-порты {Udp}");

                logger.Information($"The value for host is: {Host}");
                logger.Information($"The value for test is: {Test}");
                logger.Information($"The value for tcpPorts is: {Tcp}");
                logger.Information($"The value for udpPorts is: {Udp}");
                logger.Information($"The value for verbose is: {Verbose}");

                var builder = WebApplication.CreateBuilder();

                builder.Host.UseServiceProviderFactory(context => {
                    var factory = ServiceProviderFactory.Instance;
                    return ServiceProviderFactory.Instance;
                });

                builder.Services.AddSignalR();
                builder.Services.AddSingleton<IDataTransferService, DataTransferService>();
                builder.Services.AddSingleton<IRequestProcessingService, RequestProcessingService>();

                var app = builder.Build();
                app.MapHub<ChatHub>("/chatHub");
                await app.RunAsync("http://127.0.0.1:8088");
                return 0;
            }
        }
    }
}
