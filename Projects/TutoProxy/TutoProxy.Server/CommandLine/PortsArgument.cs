using System.CommandLine;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TutoProxy.Server.Tests")]
namespace TutoProxy.Server.CommandLine {
    internal class PortsArgument {
        public List<int> Ports { get; set; } = new List<int>();

        public override string ToString() {
            return String.Join(',', Ports.Select(x => x.ToString()).ToArray());
        }

        public static Option<PortsArgument?> CreateOption(string name) {
            return new Option<PortsArgument?>(
                     name: name,
                     parseArgument: (result) => {
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
                     description: $"Listened ports, format like '{name}=80,81,443,700-900'"
             );
        }

        public static PortsArgument Parse(string value) {
            Guard.NotNullOrEmpty(value, nameof(value));
            var postArgument = new PortsArgument();
            var tokens = value.Split(',');
            foreach(var token in tokens) {
                var ranges = ParseRanges(token);
                if(ranges != null) {
                    postArgument.Ports.AddRange(ranges);
                } else {
                    postArgument.Ports.Add(ParsePort(token));
                }
            }
            return postArgument;
        }

        static IEnumerable<int>? ParseRanges(string token) {
            var ranges = token.Split('-');
            if(ranges.Length != 2) {
                return null;
            }
            int startVal = ParsePort(ranges[0]);
            int endVal = ParsePort(ranges[1]);
            if(startVal >= endVal) {
                throw new ArgumentException($"Ranges '{token}' are incorrect");
            }
            return Enumerable.Range(startVal, (endVal - startVal) + 1);
        }

        static int ParsePort(string token) {
            if(!int.TryParse(token, out int port) || port < 1 || port > 65535 || port.ToString() != token) {
                throw new ArgumentException($"Port value '{token}' is incorrect");
            }
            return port;
        }
    }
}
