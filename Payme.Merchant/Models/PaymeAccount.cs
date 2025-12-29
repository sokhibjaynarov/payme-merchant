using Newtonsoft.Json;

namespace Payme.Merchant.Models
{
    public class PaymeAccount
    {
        // Payme sends account parameters dynamically. 
        // We enforce order_id presence.
        [JsonProperty("order_id")]
        public string? OrderId { get; set; }

        // Users might add other fields, but we focus on order_id.
        // We could use [JsonExtensionData] if we wanted to support arbitrary fields, 
        // but for now strict mapping for OrderId is crucial.
    }
}
