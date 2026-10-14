namespace Payme.Merchant.Models
{
    public enum TransactionState
    {
        Created = 1,
        Completed = 2,
        Cancelled = -1,
        CancelledAfterComplete = -2
    }

    /// <summary>Cancel reasons from the Payme spec.</summary>
    public static class CancelReason
    {
        public const int ReceiverNotFound = 1;
        public const int ProcessingError = 2;
        public const int ExecutionError = 3;
        public const int Timeout = 4;
        public const int Refund = 5;
        public const int Unknown = 10;
    }
}
