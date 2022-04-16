using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
using TutoProxy.Server.CommandLine;

class Program {
    public static async Task<int> Main(string[] args) {
        var hostArg = new Argument<string>("host", "Local host address");
        var postArg = new Argument<PortsArgument?>(
                name: "ports",
                parse: (result) => {
                    if(result.Tokens.Count != 1) {
                        result.ErrorMessage = "Ports can only be parsed with single token";
                        return default;
                    }
                    try {
                        return PortsArgument.Parse(result.Tokens[0].Value);
                    } catch(ArgumentException exception) {
                        result.ErrorMessage = exception.GetBaseException().Message;
                        return default;
                    }
                },
                description: "Listened port (80,81,443,700-900)"
                );

        var testArg = new Argument<string>("test");
        var verboseOpt = new Option<bool>("--verbose", "Show the verbose logs");
        var rootCommand = new RootCommand {
            hostArg,
            postArg,
            testArg,
            verboseOpt,
        };

        rootCommand.Description = "Прокси сервер TuTo";

        rootCommand.SetHandler((string host, PortsArgument ports, string test, bool verbose) => {
            Console.WriteLine($"The value for host is: {host}");
            Console.WriteLine($"The value for ports is: {ports}");
            Console.WriteLine($"The value for test is: {test}");
            Console.WriteLine($"The value for verbose is: {verbose}");

            StartServer(host, ports, verbose);
        }, hostArg, postArg, testArg, verboseOpt);

        return await rootCommand.InvokeAsync(args);
    }

    static void StartServer(string host, PortsArgument ports, bool verbose) {
        Console.WriteLine("{0} {1}", Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version);
        Console.WriteLine("Прокси сервер TuTo, хост {0}, портs {1}", host, ports);
    }
}