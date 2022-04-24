using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Прокси клиент TuTo") {
            Add(new Argument<string>("server", "Remote server address"));
        }

        public new class Handler : ICommandHandler {
            readonly IServiceProvider serviceProvider;
            readonly ILogger logger;

            public string? Server { get; set; }
            HubConnection? connection;

            public Handler(
                IServiceProvider serviceProvider,
                ILogger logger) {
                Guard.NotNull(serviceProvider, nameof(serviceProvider));
                Guard.NotNull(logger, nameof(logger));
                this.serviceProvider = serviceProvider;
                this.logger = logger;
            }

            public async Task<int> InvokeAsync(InvocationContext context) {
                if(string.IsNullOrEmpty(Server)) {
                    return -1;
                }

                logger.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                logger.Information($"Прокси клиент TuTo, сервер {Server}");

                connection = new HubConnectionBuilder()
                    .WithUrl("http://127.0.0.1:8088/ChatHub")
                    .Build();

                connection.On<string, string>("ReceiveMessage", (user, message) => {
                    logger.Information($"{user}: {message}");
                });

                await connection.StartAsync();
                logger.Information("Connection started");


                logger.Information("Введите свой псевдоним");
                var nickName = Console.ReadLine();
                logger.Information("Вы подключены к чату под именем {0}", nickName);
                while(true) {
                    var line = Console.ReadLine();
                    if(line == "quit") { break; }
                    await connection.InvokeAsync("SendMessage", nickName, line);
                }
                return 0;
            }
        }
    }
}
