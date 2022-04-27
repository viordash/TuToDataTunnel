using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public class DataRequestModel : DataBaseModel {
        public string Protocol { get; set; } = string.Empty;

        public override string ToString() {
            return $"{base.ToString()}, prot:{Protocol}";
        }
    }
}
