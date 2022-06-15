namespace TuToProxy.Core.Exceptions {
    public class HubClientNotFoundException : TuToException {
        public HubClientNotFoundException(string connectionId) : base($"hub-client ({connectionId}) not found") {
        }
    }
}
