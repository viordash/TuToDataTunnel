using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Reflection;

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
                if(!result.Children.Any(x => x.GetValueForOption(tcpOption) != null || x.GetValueForOption(udpOption) != null)) {
                    result.ErrorMessage = "tcp or udp options requried";
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
                if(Host == null || (Tcp == null && Udp == null)) {
                    return -1;
                }

                Console.WriteLine("{0} {1}", Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version);
                Console.WriteLine("Прокси сервер TuTo, хост {0}, tcp-порты {1}, udp-порты {2}", Host, Tcp, Udp);

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
