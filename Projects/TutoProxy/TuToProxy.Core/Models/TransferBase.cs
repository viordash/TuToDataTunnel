namespace TutoProxy.Core.Models {
    public abstract class TransferBase {
        public string Id { get; private set; }
        public DateTime DateTime { get; private set; }

        protected TransferBase(string id, DateTime dateTime) {
            Id = id;
            DateTime = dateTime;
        }

        public override string ToString() {
            return $"Id: '{Id}', DateTime: {DateTime}";
        }
    }
}
