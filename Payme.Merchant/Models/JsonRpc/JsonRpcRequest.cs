using Newtonsoft.Json;
using System;

namespace Payme.Merchant.Models.JsonRpc
{
    public class JsonRpcRequest
    {
        [JsonProperty("method")]
        public string Method { get; set; } = default!;

        [JsonProperty("params")]
        public object Params { get; set; } = default!;

        [JsonProperty("id")]
        public object Id { get; set; } = default!;
    }
}
