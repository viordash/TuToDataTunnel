﻿namespace TutoProxy.Core.Models {
    public abstract class TransferBase {
        public string Id { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }

        public override string ToString() {
            return $"Id: '{Id}', DateTime: {DateTime}";
        }
    }
}