using Newtonsoft.Json;
using System.Collections.Generic;

namespace Payme.Merchant.Models.MethodDtos
{
    public class CheckPerformTransactionResult
    {
        [JsonProperty("allow")]
        public bool Allow { get; set; }

        [JsonProperty("detail")]
        public object? Detail { get; set; } // Items, etc. (Fiscalization)

        // Additional fiscal fields if needed, but Payme mostly looks for 'allow'. 
        // Detail can contain 'receipt_type', 'items', etc.
    }

    public class CreateTransactionResult
    {
        [JsonProperty("create_time")]
        public long CreateTime { get; set; }

        [JsonProperty("transaction")]
        public string Transaction { get; set; } = default!; // ID

        [JsonProperty("state")]
        public int State { get; set; } // TransactionState
    }

    public class PerformTransactionResult
    {
        [JsonProperty("perform_time")]
        public long PerformTime { get; set; }

        [JsonProperty("transaction")]
        public string Transaction { get; set; } = default!;

        [JsonProperty("state")]
        public int State { get; set; }
    }

    public class CancelTransactionResult
    {
        [JsonProperty("cancel_time")]
        public long CancelTime { get; set; }

        [JsonProperty("transaction")]
        public string Transaction { get; set; } = default!;

        [JsonProperty("state")]
        public int State { get; set; }
    }

    public class CheckTransactionResult
    {
        [JsonProperty("create_time")]
        public long CreateTime { get; set; }

        [JsonProperty("perform_time")]
        public long PerformTime { get; set; }

        [JsonProperty("cancel_time")]
        public long CancelTime { get; set; }

        [JsonProperty("transaction")]
        public string Transaction { get; set; } = default!;

        [JsonProperty("state")]
        public int State { get; set; }

        [JsonProperty("reason")]
        public int? Reason { get; set; }
    }

    public class GetStatementResult
    {
        [JsonProperty("transactions")]
        public List<CheckTransactionResult> Transactions { get; set; } = new List<CheckTransactionResult>();
    }
}
