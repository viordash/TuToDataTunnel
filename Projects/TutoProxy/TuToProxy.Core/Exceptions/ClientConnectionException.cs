namespace TuToProxy.Core.Exceptions {
    public class ClientConnectionException : TuToException {
        public ClientConnectionException(string connectionId, string message) : base($"Client :{connectionId}, '{message}'") {

        }
    }
}
