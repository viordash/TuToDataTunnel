using System.CommandLine.Parsing;

namespace TutoProxy.Server.CommandLine {
    internal class PortsArgument {
        public List<int> Ports { get; set; } = new List<int>();

        public static ParseArgument<PortsArgument?> ToParseArgument() =>
            result => Parse(result);

        static PortsArgument? Parse(ArgumentResult result) {
            if(result.Tokens.Count != 1) {
                result.ErrorMessage = "Ports can only be parsed with single token";
                return default;
            }
            var token = result.Tokens[0];
            var postArgument = new PortsArgument();

            return postArgument;
        }
    }
}
