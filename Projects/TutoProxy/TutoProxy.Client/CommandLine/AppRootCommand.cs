using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using TutoProxy.Core.CommandLine;

namespace TutoProxy.Server.CommandLine {
    internal class AppRootCommand : RootCommand {
        public AppRootCommand() : base("Прокси клиент TuTo") {
            Add(new Argument<string>("server", "Remote server address"));
        }

        public new class Handler : ICommandHandler {
            public string? Server { get; set; }

            public async Task<int> InvokeAsync(InvocationContext context) {
                if(string.IsNullOrEmpty(Server)) {
                    return -1;
                }

                Log.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                Log.Information($"Прокси клиент TuTo, сервер {Server}");

                return 0;
            }
        }
    }
}
