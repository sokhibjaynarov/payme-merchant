using Newtonsoft.Json;
using System.Collections.Generic;

namespace Payme.Merchant.Models.JsonRpc
{
    public class JsonRpcError
    {
        [JsonProperty("code")] public int Code { get; set; }

        [JsonProperty("message")] public object Message { get; set; }

        [JsonProperty("data")] public object? Data { get; set; }

        public JsonRpcError(int code, object message, object? data = null)
        {
            Code = code;
            Message = message;
            Data = data;
        }
    }
}
