using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
