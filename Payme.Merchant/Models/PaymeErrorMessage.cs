using Newtonsoft.Json;

namespace Payme.Merchant.Models
{
    public class PaymeErrorMessage
    {
        [JsonProperty("ru")]
        public string Ru { get; set; }

        [JsonProperty("uz")]
        public string Uz { get; set; }

        [JsonProperty("en")]
        public string En { get; set; }

        public PaymeErrorMessage(string ru, string uz, string en)
        {
            Ru = ru;
            Uz = uz;
            En = en;
        }
    }
}
