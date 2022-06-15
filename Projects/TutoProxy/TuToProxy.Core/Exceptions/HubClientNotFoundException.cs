using TuToProxy.Core.Models;

namespace TuToProxy.Core.Exceptions {
    public class HubClientNotFoundException : TuToException {
        public HubClientNotFoundException(string connectionId) : base($"hub-client ({connectionId}) not found") {
        }
        public HubClientNotFoundException(DataProtocol protocol, int port) : base($"hub-client for {protocol}({port}) not found") {
        }
    }
}
