namespace TuToProxy.Core.Services {
    public interface IIdService {
        public string TransferRequest { get; }
    }
    public class IdService : IIdService {
        public string TransferRequest {
            get {
                return Guid.NewGuid().ToString();
            }
        }
    }
}
