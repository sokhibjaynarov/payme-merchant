using Newtonsoft.Json;

namespace Payme.Merchant.Models.JsonRpc
{
    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")] public string Version { get; } = "2.0";

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object? Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public JsonRpcError? Error { get; set; }

        [JsonProperty("id")] public object Id { get; set; }

        public JsonRpcResponse(object id, object? result)
        {
            Id = id;
            Result = result;
            Error = null;
        }

        public JsonRpcResponse(object id, JsonRpcError error)
        {
            Id = id;
            Result = null;
            Error = error;
        }
    }
}
