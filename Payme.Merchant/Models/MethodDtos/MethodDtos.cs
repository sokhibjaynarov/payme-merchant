using Newtonsoft.Json;
using Payme.Merchant.Models;

namespace Payme.Merchant.Models.MethodDtos
{
    public class CheckPerformTransactionDto
    {
        [JsonProperty("amount")]
        public long Amount { get; set; }

        [JsonProperty("account")]
        public PaymeAccount Account { get; set; } = default!;
    }

    public class CreateTransactionDto
    {
        [JsonProperty("id")]
        public string Id { get; set; } = default!;

        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("amount")]
        public long Amount { get; set; }

        [JsonProperty("account")]
        public PaymeAccount Account { get; set; } = default!;
    }

    public class PerformTransactionDto
    {
        [JsonProperty("id")]
        public string Id { get; set; } = default!;
    }

    public class CancelTransactionDto
    {
        [JsonProperty("id")]
        public string Id { get; set; } = default!;

        [JsonProperty("reason")]
        public int Reason { get; set; }
    }

    public class CheckTransactionDto
    {
        [JsonProperty("id")]
        public string Id { get; set; } = default!;
    }

    public class GetStatementDto
    {
        [JsonProperty("from")]
        public long From { get; set; }

        [JsonProperty("to")]
        public long To { get; set; }
    }
}
