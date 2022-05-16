namespace TutoProxy.Core.Models {
    public abstract class TransferBase {
        public string Id { get; private set; }
        public DateTime Created { get; private set; }

        protected TransferBase(string id, DateTime created) {
            Id = id;
            Created = created;
        }

        public override string ToString() {
            return $"Id: '{Id}', Created: {Created}";
        }
    }
}
