using TuToProxy.Core.Models;

namespace TuToProxy.Core.Exceptions {
    public class SocketPortNotBoundException : TuToException {
        public SocketPortNotBoundException(DataProtocol protocol, int port) : base($"{protocol} socket port({port}) not bound") {

        }
    }
}
