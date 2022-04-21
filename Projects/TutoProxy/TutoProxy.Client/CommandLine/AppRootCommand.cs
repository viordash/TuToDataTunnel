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
            public string? Server { get; set; }
            HubConnection? connection;

            public async Task<int> InvokeAsync(InvocationContext context) {
                if(string.IsNullOrEmpty(Server)) {
                    return -1;
                }

                Log.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                Log.Information($"Прокси клиент TuTo, сервер {Server}");

                connection = new HubConnectionBuilder()
                    .WithUrl("http://127.0.0.1:8088/ChatHub")
                    .Build();

                connection.On<string, string>("ReceiveMessage", (user, message) => {
                    Log.Information($"{user}: {message}");
                });

                await connection.StartAsync();
                Log.Information("Connection started");


                Console.WriteLine("Введите свой псевдоним");
                var nickName = Console.ReadLine();
                Console.WriteLine("Вы подключены к чату под именем {0}", nickName);
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
