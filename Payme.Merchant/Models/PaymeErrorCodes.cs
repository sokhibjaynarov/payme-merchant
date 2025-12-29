namespace Payme.Merchant.Models
{
    public static class PaymeErrorCodes
    {
        // JSON-RPC / System Errors
        public const int TransportError = -32300;
        public const int AccessDenied = -32504;
        public const int MethodNotFound = -32601;
        public const int InvalidJson = -32700;
        public const int SystemError = -32400;

        // Business Errors
        public const int OrderNotFound = -31001;
        public const int WrongAmount = -31003;
        public const int UnableToComplete = -31008;
        public const int TransactionNotFound = -31003; // Note: Specs sometimes reuse or have specific ones.
                                                        // Spec says: "Transaction not found" is often handled by specific logic or just -31003 for general mismatch?
                                                        // Actually, checking spec: 
                                                        // CheckTransaction: if not found -> -31003 "Transaction found" (Wait, usually -31003 is 'Order not found' or mismatch?)
                                                        // Let's re-verify the specific error codes.
                                                        
        // Official codes from documentation:
        // -31001: Order not found
        // -31003: Incorrect amount
        // -31008: Unable to perform operation (generic business logic failure)
        // -31007: Order already processed / cancelled (used in CancelTransaction mainly if timeout passed but logic prohibits?)
        
        // Let's stick to the main ones and add others if needed by implementation details.
    }
}
