using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
using TutoProxy.Server.CommandLine;

class Program {
    public static async Task<int> Main(string[] args) {
        var hostArg = new Argument<string>("host", "Local host address");
        var testArg = new Argument<string>("test");
        var udpPortsOptions = PortsArgument.CreateOption("--udp");
        var tcpPortsOptions = PortsArgument.CreateOption("--tcp");

        var verboseOpt = new Option<bool>("--verbose", "Show the verbose logs");
        var rootCommand = new RootCommand {
            hostArg,
            testArg,
            tcpPortsOptions,
            udpPortsOptions,
            verboseOpt,
        };

        rootCommand.Description = "Прокси сервер TuTo";

        rootCommand.SetHandler((string host, string test, PortsArgument tcpPorts, PortsArgument udpPorts, bool verbose) => {
            Console.WriteLine($"The value for host is: {host}");
            Console.WriteLine($"The value for test is: {test}");
            Console.WriteLine($"The value for tcpPorts is: {tcpPorts}");
            Console.WriteLine($"The value for udpPorts is: {udpPorts}");
            Console.WriteLine($"The value for verbose is: {verbose}");

            StartServer(host, tcpPorts, udpPorts, verbose);
        }, hostArg, testArg, tcpPortsOptions, udpPortsOptions, verboseOpt);

        return await rootCommand.InvokeAsync(args);
    }

    static void StartServer(string host, PortsArgument tcpPorts, PortsArgument udpPorts, bool verbose) {
        Console.WriteLine("{0} {1}", Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version);
        Console.WriteLine("Прокси сервер TuTo, хост {0}, tcp-порты {1}, udp-порты {2}", host, tcpPorts, udpPorts);
    }
}