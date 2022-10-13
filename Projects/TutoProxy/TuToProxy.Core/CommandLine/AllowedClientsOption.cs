using System.CommandLine;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TutoProxy.Server.Tests")]
[assembly: InternalsVisibleTo("TuToProxy.Core.Tests")]
namespace TuToProxy.Core.CommandLine {
    public class AllowedClientsOption {
        public List<string> Clients { get; set; } = new List<string>();
        public string Option { get; private set; }

        AllowedClientsOption(string option) {
            Option = option;
        }

        public override string ToString() {
            return string.Join(',', Clients.Select(x => x.ToString()).ToArray());
        }

        public static Option<AllowedClientsOption?> Create(string name, string description) {
            return new Option<AllowedClientsOption?>(
                     name: name,
                     parseArgument: (result) => {
                         if(result.Tokens.Count != 1) {
                             result.ErrorMessage = "Value only be parsed with single token";
                             return default;
                         }
                         try {
                             return Parse(result.Tokens[0].Value);
                         } catch(ArgumentException exception) {
                             result.ErrorMessage = exception.GetBaseException().Message;
                             return default;
                         }
                     },
                     description: description
             );
        }

        public static AllowedClientsOption Parse(string? value) {
            Guard.NotNullOrEmpty(value, nameof(value));
            var option = new AllowedClientsOption(value!);
            var tokens = value!.Split(',');
            foreach(var token in tokens) {
                option.Clients.Add(token);
            }
            return option;
        }
    }
}
