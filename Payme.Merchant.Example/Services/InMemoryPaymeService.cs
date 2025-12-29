using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Payme.Merchant.Abstractions;
using Payme.Merchant.Models;

namespace Payme.Merchant.Example.Services
{
    public class InMemoryPaymeService : IPaymeOrderService, IPaymeTransactionStore
    {
        // Mock Data
        private readonly ConcurrentDictionary<string, Order> _orders = new();
        private readonly ConcurrentDictionary<string, PaymeTransaction> _transactions = new();

        public InMemoryPaymeService()
        {
            // Seed some orders
            _orders.TryAdd("1001", new Order { Id = "1001", Amount = 100000, IsPaid = false }); // 1000.00 UZS
            _orders.TryAdd("1002", new Order { Id = "1002", Amount = 500000, IsPaid = false }); // 5000.00 UZS
        }

        // --- Order Service ---

        public Task<bool> ExistsAsync(string orderId)
        {
            return Task.FromResult(_orders.ContainsKey(orderId));
        }

        public Task<long> GetAmountAsync(string orderId)
        {
            if (_orders.TryGetValue(orderId, out var order))
            {
                return Task.FromResult(order.Amount);
            }
            return Task.FromResult(0L);
        }

        public Task<bool> IsPaidAsync(string orderId)
        {
            if (_orders.TryGetValue(orderId, out var order))
            {
                return Task.FromResult(order.IsPaid);
            }
            return Task.FromResult(false);
        }

        public Task MarkAsPaidAsync(string orderId)
        {
            if (_orders.TryGetValue(orderId, out var order))
            {
                order.IsPaid = true;
            }
            return Task.CompletedTask;
        }

        public Task CancelAsync(string orderId, int reason)
        {
            if (_orders.TryGetValue(orderId, out var order))
            {
                // Business logic: e.g. unblock items if needed.
                // If it was paid, maybe refund logic here.
                order.IsPaid = false; // Just for demo
            }
            return Task.CompletedTask;
        }

        // --- Transaction Store ---

        public Task CreateAsync(string paymeTransactionId, string orderId, long time, long amount)
        {
            var t = new PaymeTransaction
            {
                Id = paymeTransactionId,
                OrderId = orderId,
                Time = time,
                Amount = amount,
                CreateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                State = (int)TransactionState.Created
            };
            _transactions.TryAdd(paymeTransactionId, t);
            return Task.CompletedTask;
        }

        public Task<PaymeTransaction?> GetAsync(string paymeTransactionId)
        {
            _transactions.TryGetValue(paymeTransactionId, out var t);
            return Task.FromResult(t);
        }

        public Task PerformAsync(string paymeTransactionId, long performTime)
        {
            if (_transactions.TryGetValue(paymeTransactionId, out var t))
            {
                t.State = (int)TransactionState.Completed;
                t.PerformTime = performTime;
            }
            return Task.CompletedTask;
        }

        public Task CancelAsync(string paymeTransactionId, long cancelTime, int reason, int newState)
        {
            if (_transactions.TryGetValue(paymeTransactionId, out var t))
            {
                t.State = newState; // -1 or -2
                t.CancelTime = cancelTime;
                t.Reason = reason;
            }
            return Task.CompletedTask;
        }

        private class Order
        {
            public string Id { get; set; } = default!;
            public long Amount { get; set; }
            public bool IsPaid { get; set; }
        }
    }
}
