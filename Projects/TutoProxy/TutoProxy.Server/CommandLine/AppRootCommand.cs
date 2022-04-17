using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using TutoProxy.Core.CommandLine;

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
            public string? Host { get; set; }
            public string? Test { get; set; }
            public PortsArgument? Udp { get; set; }
            public PortsArgument? Tcp { get; set; }
            public bool Verbose { get; set; }

            public async Task<int> InvokeAsync(InvocationContext context) {
                if(string.IsNullOrEmpty(Host) || (Tcp == null && Udp == null)) {
                    return -1;
                }

                Log.Information($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
                Log.Information($"Прокси сервер TuTo, хост {Host}, tcp-порты {Tcp}, udp-порты {Udp}");

                Console.WriteLine($"The value for host is: {Host}");
                Console.WriteLine($"The value for test is: {Test}");
                Console.WriteLine($"The value for tcpPorts is: {Tcp}");
                Console.WriteLine($"The value for udpPorts is: {Udp}");
                Console.WriteLine($"The value for verbose is: {Verbose}");

                return 0;
            }
        }
    }
}
