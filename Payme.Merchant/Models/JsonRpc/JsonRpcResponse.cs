using Newtonsoft.Json;

namespace Payme.Merchant.Models.JsonRpc
{
    public class JsonRpcResponse
    {
        [JsonProperty("result")]
        public object? Result { get; set; }

        [JsonProperty("error")]
        public JsonRpcError? Error { get; set; }

        [JsonProperty("id")]
        public object Id { get; set; }

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
