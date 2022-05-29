using TuToProxy.Core.Models;

namespace TuToProxy.Core.Exceptions {
    public class ClientNotFoundException : TuToException {
        public ClientNotFoundException(DataProtocol protocol, int port) : base($"client ({protocol}:{port}) not found") {

        }
    }
}
