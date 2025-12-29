namespace Payme.Merchant.Models
{
    public enum TransactionState
    {
        Created = 1,
        Completed = 2,
        Cancelled = -1,
        CancelledAfterComplete = -2
    }
}
