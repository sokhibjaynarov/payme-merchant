using System.Collections.Generic;
using System.Threading.Tasks;

namespace Payme.Merchant.Abstractions
{
    /// <summary>Your side of the deal: the thing being paid for (an order, an invoice, a wallet top-up).</summary>
    public interface IPaymeOrderService
    {
        Task<bool> ExistsAsync(string orderId);

        /// <summary>True when the order can no longer accept a payment (already paid, expired, cancelled).</summary>
        Task<bool> IsPaidAsync(string orderId);

        /// <summary>Amount in tiyin (1 so'm = 100 tiyin), exactly as Payme sends it.</summary>
        Task<long> GetAmountAsync(string orderId);

        Task MarkAsPaidAsync(string orderId);

        /// <summary>Called when a transaction is cancelled, before or after it was performed.</summary>
        Task CancelAsync(string orderId, int reason);

        /// <summary>
        /// Whether a performed (paid) transaction may still be reversed, e.g. the goods were not shipped or the
        /// credited balance is still unspent. Return false to answer Payme with -31007.
        /// </summary>
        Task<bool> CanCancelAfterPerformAsync(string orderId) => Task.FromResult(false);

        /// <summary>
        /// Fiscal receipt data returned as <c>detail</c> in CheckPerformTransaction (receipt_type, items with
        /// IKPU code, package code, VAT). Required by Payme for fiscalised merchants; null to omit.
        /// </summary>
        Task<object?> GetDetailAsync(string orderId) => Task.FromResult<object?>(null);
    }

    public interface IPaymeTransactionStore
    {
        /// <param name="paymeTime">The <c>time</c> Payme sent (when the payment started on their side).</param>
        /// <param name="createTime">Our own clock when the transaction was recorded; returned as create_time.</param>
        Task CreateAsync(string paymeTransactionId, string orderId, long paymeTime, long amount, long createTime);
        Task<PaymeTransaction?> GetAsync(string paymeTransactionId);

        /// <summary>The order's latest transaction that is not cancelled, or null.</summary>
        Task<PaymeTransaction?> GetByOrderIdAsync(string orderId);
        Task PerformAsync(string paymeTransactionId, long performTime);
        Task CancelAsync(string paymeTransactionId, long cancelTime, int reason, int newState);

        /// <summary>Transactions whose Payme <c>time</c> is within [from, to], for GetStatement.</summary>
        Task<IReadOnlyList<PaymeTransaction>> GetByPaymeTimeRangeAsync(long from, long to);
    }

    public class PaymeTransaction
    {
        public string Id { get; set; } = default!;
        public string OrderId { get; set; } = default!;
        /// <summary>Payme's <c>time</c>.</summary>
        public long Time { get; set; }
        public long Amount { get; set; }
        public int State { get; set; }
        public long CreateTime { get; set; }
        public long PerformTime { get; set; }
        public long CancelTime { get; set; }
        public int? Reason { get; set; }
    }
}
