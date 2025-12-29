using System.Threading.Tasks;
using Payme.Merchant.Models;

namespace Payme.Merchant.Abstractions
{
    public interface IPaymeOrderService
    {
        Task<bool> ExistsAsync(string orderId);
        Task<bool> IsPaidAsync(string orderId);
        Task<long> GetAmountAsync(string orderId); // tiyin
        Task MarkAsPaidAsync(string orderId);
        Task CancelAsync(string orderId, int reason);
    }

    public interface IPaymeTransactionStore
    {
        Task CreateAsync(string paymeTransactionId, string orderId, long time, long amount);
        Task<PaymeTransaction?> GetAsync(string paymeTransactionId);
        Task PerformAsync(string paymeTransactionId, long performTime);
        Task CancelAsync(string paymeTransactionId, long cancelTime, int reason, int newState);

        // For GetStatement if needed, or we just return list
        // Task<List<PaymeTransaction>> GetStatementAsync(long from, long to);
    }

    // We need a PaymeTransaction model for the store to return.
    public class PaymeTransaction
    {
        public string Id { get; set; } = default!; // Payme ID
        public string OrderId { get; set; } = default!;
        public long Time { get; set; } // Create Time
        public long Amount { get; set; }
        public int State { get; set; }
        public long CreateTime { get; set; }
        public long PerformTime { get; set; }
        public long CancelTime { get; set; }
        public int Reason { get; set; }
    }
}
