using System.Collections.Generic;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Payme.Merchant.Abstractions;
using Payme.Merchant.Models;

namespace Payme.Merchant.Example.Services
{
    public class InMemoryPaymeService : IPaymeOrderService, IPaymeTransactionStore
    {
        private readonly ConcurrentDictionary<string, Order> _orders = new();
        private readonly ConcurrentDictionary<string, PaymeTransaction> _transactions = new();

        public InMemoryPaymeService()
        {
            _orders.TryAdd("1001", new Order { Id = "1001", Amount = 100000, IsPaid = false });
            _orders.TryAdd("1002", new Order { Id = "1002", Amount = 500000, IsPaid = false });
        }

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
                order.IsPaid = false;
            }

            return Task.CompletedTask;
        }

        public Task CreateAsync(string paymeTransactionId, string orderId, long time, long amount, long createTime)
        {
            var t = new PaymeTransaction
            {
                Id = paymeTransactionId,
                OrderId = orderId,
                Time = time,
                Amount = amount,
                CreateTime = createTime,
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

        public Task<PaymeTransaction?> GetByOrderIdAsync(string orderId)
        {
            var t = _transactions.Values.Where(x => x.OrderId == orderId).OrderByDescending(x => x.CreateTime).FirstOrDefault();
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
                t.State = newState;
                t.CancelTime = cancelTime;
                t.Reason = reason;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PaymeTransaction>> GetByPaymeTimeRangeAsync(long from, long to)
        {
            IReadOnlyList<PaymeTransaction> list = _transactions.Values.Where(x => x.Time >= from && x.Time <= to).ToList();
            return Task.FromResult(list);
        }

        private class Order
        {
            public string Id { get; set; } = default!;
            public long Amount { get; set; }
            public bool IsPaid { get; set; }
        }
    }
}
