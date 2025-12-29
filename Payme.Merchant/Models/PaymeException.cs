using System;

namespace Payme.Merchant.Models
{
    public class PaymeException : Exception
    {
        public int Code { get; }
        public new string Message { get; } // JSON-RPC message
        public string? Data { get; } // Optional "data" field

        public PaymeException(int code, string message, string? data = null) : base(message)
        {
            Code = code;
            Message = message;
            Data = data;
        }
    }
}
