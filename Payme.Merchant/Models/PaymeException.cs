using System;
using Newtonsoft.Json;

namespace Payme.Merchant.Models
{
    public class PaymeException : Exception
    {
        public int Code { get; }
        public new object Message { get; } // JSON-RPC message
        public new string? Data { get; } // Optional "data" field

        public PaymeException(int code, object message, string? data = null) : base(
            JsonConvert.SerializeObject(message))
        {
            Code = code;
            Message = message;
            Data = data;
        }

        public PaymeException(int code, string message, string? data = null) : base(message)
        {
            Code = code;
            Message = message;
            Data = data;
        }
    }
}
