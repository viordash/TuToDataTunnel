using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TutoProxy.Client.Services;
using TutoProxy.Core.Models;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Прокси клиент TuTo") {
            Add(new Argument<string>("server", "Remote server address"));
        }

        public new class Handler : ICommandHandler {
            readonly ILogger logger;
            readonly IDataReceiveService dataReceiveService;
            HubConnection? connection;

            public string? Server { get; set; }

            public Handler(
                ILogger logger,
                IDataReceiveService dataReceiveService
                ) {
                Guard.NotNull(logger, nameof(logger));
                Guard.NotNull(dataReceiveService, nameof(dataReceiveService));
                this.logger = logger;
                this.dataReceiveService = dataReceiveService;
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

                connection.On<TransferRequestModel>("DataRequest", async (request) => {
                    var response = dataReceiveService.HandleRequest(request);
                    await connection.InvokeAsync("Response", response);
                });


                await connection.StartAsync();
                logger.Information("Connection started");

                while(true) {
                    var line = Console.ReadLine();
                    if(line == "quit") { break; }
                    await connection.InvokeAsync("SendMessage", connection.ConnectionId, "nickName", line);
                }
                return 0;
            }
        }
    }
}
