using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Payme.Merchant.Abstractions;
using Payme.Merchant.Configuration;
using Payme.Merchant.Models;
using Payme.Merchant.Models.JsonRpc;
using Payme.Merchant.Models.MethodDtos;

namespace Payme.Merchant
{
    /// <summary>
    /// Routes one Payme Merchant API call (JSON-RPC 2.0) to your order service and transaction store.
    /// Always answer HTTP 200 with the returned JSON; Payme reads errors from the body, not the status.
    /// </summary>
    public class PaymeDispatcher
    {
        private readonly PaymeOptions _options;
        private readonly IPaymeOrderService _orders;
        private readonly IPaymeTransactionStore _store;
        private readonly TimeProvider _time;
        private readonly ILogger<PaymeDispatcher>? _logger;

        public PaymeDispatcher(
            IOptions<PaymeOptions> options,
            IPaymeOrderService orderService,
            IPaymeTransactionStore transactionStore,
            TimeProvider? timeProvider = null,
            ILogger<PaymeDispatcher>? logger = null)
        {
            _options = options.Value;
            _orders = orderService;
            _store = transactionStore;
            _time = timeProvider ?? TimeProvider.System;
            _logger = logger;
        }

        private long Now => _time.GetUtcNow().ToUnixTimeMilliseconds();
        private long TimeoutMs => (long)_options.TransactionTimeout.TotalMilliseconds;

        public async Task<string> DispatchAsync(string? authHeader, string requestBody)
        {
            object? id = null;
            try
            {
                JsonRpcRequest? request;
                try
                {
                    request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestBody);
                }
                catch (JsonException)
                {
                    throw new PaymeException(PaymeErrorCodes.InvalidJson, "Parse error");
                }

                id = request?.Id;

                // Payme's own test suite expects -32504 even for a well-formed request, so auth comes after
                // parsing only to be able to echo the request id back.
                if (!IsAuthorized(authHeader))
                    throw new PaymeException(PaymeErrorCodes.AccessDenied, "Insufficient privileges");

                if (request == null || string.IsNullOrEmpty(request.Method) || request.Params == null)
                    throw new PaymeException(PaymeErrorCodes.InvalidRequest, "Invalid Request");

                object result = request.Method switch
                {
                    "CheckPerformTransaction" => await CheckPerformTransactionAsync(Params<CheckPerformTransactionDto>(request.Params)),
                    "CreateTransaction" => await CreateTransactionAsync(Params<CreateTransactionDto>(request.Params)),
                    "PerformTransaction" => await PerformTransactionAsync(Params<PerformTransactionDto>(request.Params)),
                    "CancelTransaction" => await CancelTransactionAsync(Params<CancelTransactionDto>(request.Params)),
                    "CheckTransaction" => await CheckTransactionAsync(Params<CheckTransactionDto>(request.Params)),
                    "GetStatement" => await GetStatementAsync(Params<GetStatementDto>(request.Params)),
                    _ => throw new PaymeException(PaymeErrorCodes.MethodNotFound, "Method not found", request.Method)
                };

                return JsonConvert.SerializeObject(new JsonRpcResponse(id ?? 0, result));
            }
            catch (PaymeException pe)
            {
                return JsonConvert.SerializeObject(new JsonRpcResponse(id ?? 0, new JsonRpcError(pe.Code, pe.Message, pe.Data)));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "System error processing Payme request");
                return JsonConvert.SerializeObject(new JsonRpcResponse(id ?? 0,
                    new JsonRpcError(PaymeErrorCodes.SystemError, "Internal System Error")));
            }
        }

        private bool IsAuthorized(string? authHeader)
        {
            if (string.IsNullOrEmpty(authHeader) || !authHeader!.StartsWith("Basic ", StringComparison.Ordinal))
                return false;

            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Substring(6).Trim()));
            }
            catch (FormatException)
            {
                return false;
            }

            // The key itself may contain ':'; only the first one separates login from password.
            var sep = decoded.IndexOf(':');
            if (sep < 0) return false;

            return FixedTimeEquals(decoded.Substring(0, sep), _options.Login)
                   & FixedTimeEquals(decoded.Substring(sep + 1), _options.Password ?? string.Empty);
        }

        private static bool FixedTimeEquals(string a, string b) =>
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

        private async Task<string> RequireOrderAsync(PaymeAccount? account, long amount)
        {
            var orderId = account?.OrderId;
            if (string.IsNullOrEmpty(orderId) || !await _orders.ExistsAsync(orderId!))
                throw new PaymeException(PaymeErrorCodes.OrderNotFound, PaymeErrors.OrderNotFound, "order_id");

            if (await _orders.GetAmountAsync(orderId!) != amount)
                throw new PaymeException(PaymeErrorCodes.WrongAmount, PaymeErrors.WrongAmount, "amount");

            if (await _orders.IsPaidAsync(orderId!))
                throw new PaymeException(PaymeErrorCodes.TerminalState, PaymeErrors.TerminalState, "order_id");

            return orderId!;
        }

        private async Task<CheckPerformTransactionResult> CheckPerformTransactionAsync(CheckPerformTransactionDto p)
        {
            var orderId = await RequireOrderAsync(p.Account, p.Amount);

            var active = await _store.GetByOrderIdAsync(orderId);
            if (active?.State == (int)TransactionState.Created)
                throw new PaymeException(PaymeErrorCodes.Pending, PaymeErrors.Pending, "order_id");
            if (active?.State == (int)TransactionState.Completed)
                throw new PaymeException(PaymeErrorCodes.TerminalState, PaymeErrors.TerminalState, "order_id");

            return new CheckPerformTransactionResult { Allow = true, Detail = await _orders.GetDetailAsync(orderId) };
        }

        private async Task<CreateTransactionResult> CreateTransactionAsync(CreateTransactionDto p)
        {
            var existing = await _store.GetAsync(p.Id);
            if (existing != null)
            {
                if (existing.State != (int)TransactionState.Created)
                    throw new PaymeException(PaymeErrorCodes.UnableToComplete, PaymeErrors.UnableToComplete);

                if (await CancelIfExpiredAsync(existing))
                    throw new PaymeException(PaymeErrorCodes.UnableToComplete, PaymeErrors.UnableToComplete);

                return new CreateTransactionResult { CreateTime = existing.CreateTime, Transaction = existing.Id, State = existing.State };
            }

            var orderId = await RequireOrderAsync(p.Account, p.Amount);

            // One-time order: a second Payme transaction for it while the first is open is refused.
            var active = await _store.GetByOrderIdAsync(orderId);
            if (active != null && active.Id != p.Id)
            {
                if (active.State == (int)TransactionState.Completed)
                    throw new PaymeException(PaymeErrorCodes.TerminalState, PaymeErrors.TerminalState, "order_id");
                if (active.State == (int)TransactionState.Created && !await CancelIfExpiredAsync(active))
                    throw new PaymeException(PaymeErrorCodes.Pending, PaymeErrors.Pending, "order_id");
            }

            var createTime = Now;
            await _store.CreateAsync(p.Id, orderId, p.Time, p.Amount, createTime);
            return new CreateTransactionResult { CreateTime = createTime, Transaction = p.Id, State = (int)TransactionState.Created };
        }

        private async Task<PerformTransactionResult> PerformTransactionAsync(PerformTransactionDto p)
        {
            var trans = await _store.GetAsync(p.Id)
                        ?? throw new PaymeException(PaymeErrorCodes.TransactionNotFound, PaymeErrors.TransactionNotFound);

            if (trans.State == (int)TransactionState.Completed)
                return new PerformTransactionResult { PerformTime = trans.PerformTime, Transaction = trans.Id, State = trans.State };

            if (trans.State != (int)TransactionState.Created || await CancelIfExpiredAsync(trans))
                throw new PaymeException(PaymeErrorCodes.UnableToComplete, PaymeErrors.UnableToComplete);

            var performTime = Now;
            await _orders.MarkAsPaidAsync(trans.OrderId);
            await _store.PerformAsync(trans.Id, performTime);
            return new PerformTransactionResult { PerformTime = performTime, Transaction = trans.Id, State = (int)TransactionState.Completed };
        }

        private async Task<CancelTransactionResult> CancelTransactionAsync(CancelTransactionDto p)
        {
            var trans = await _store.GetAsync(p.Id)
                        ?? throw new PaymeException(PaymeErrorCodes.TransactionNotFound, PaymeErrors.TransactionNotFound);

            if (trans.State == (int)TransactionState.Cancelled || trans.State == (int)TransactionState.CancelledAfterComplete)
                return new CancelTransactionResult { CancelTime = trans.CancelTime, Transaction = trans.Id, State = trans.State };

            int newState;
            if (trans.State == (int)TransactionState.Created)
            {
                newState = (int)TransactionState.Cancelled;
            }
            else if (trans.State == (int)TransactionState.Completed)
            {
                if (!await _orders.CanCancelAfterPerformAsync(trans.OrderId))
                    throw new PaymeException(PaymeErrorCodes.CantCancel, PaymeErrors.UnableToCancel);
                newState = (int)TransactionState.CancelledAfterComplete;
            }
            else
            {
                throw new PaymeException(PaymeErrorCodes.UnableToComplete, PaymeErrors.UnableToComplete);
            }

            var cancelTime = Now;
            await _orders.CancelAsync(trans.OrderId, p.Reason);
            await _store.CancelAsync(trans.Id, cancelTime, p.Reason, newState);
            return new CancelTransactionResult { CancelTime = cancelTime, Transaction = trans.Id, State = newState };
        }

        private async Task<CheckTransactionResult> CheckTransactionAsync(CheckTransactionDto p)
        {
            var t = await _store.GetAsync(p.Id)
                    ?? throw new PaymeException(PaymeErrorCodes.TransactionNotFound, PaymeErrors.TransactionNotFound);

            return new CheckTransactionResult
            {
                CreateTime = t.CreateTime, PerformTime = t.PerformTime, CancelTime = t.CancelTime,
                Transaction = t.Id, State = t.State, Reason = t.Reason
            };
        }

        private async Task<StatementResult> GetStatementAsync(GetStatementDto p)
        {
            var list = await _store.GetByPaymeTimeRangeAsync(p.From, p.To);
            return new StatementResult
            {
                Transactions = list.OrderBy(t => t.Time).Select(t => new StatementTransaction
                {
                    Id = t.Id, Time = t.Time, Amount = t.Amount, Account = new PaymeAccount { OrderId = t.OrderId },
                    CreateTime = t.CreateTime, PerformTime = t.PerformTime, CancelTime = t.CancelTime,
                    Transaction = t.Id, State = t.State, Reason = t.Reason
                }).ToList()
            };
        }

        /// <summary>Cancels a Created transaction older than the timeout (reason 4). True if it did.</summary>
        private async Task<bool> CancelIfExpiredAsync(PaymeTransaction t)
        {
            if (t.State != (int)TransactionState.Created || Now - t.CreateTime <= TimeoutMs)
                return false;

            await _orders.CancelAsync(t.OrderId, CancelReason.Timeout);
            await _store.CancelAsync(t.Id, Now, CancelReason.Timeout, (int)TransactionState.Cancelled);
            t.State = (int)TransactionState.Cancelled;
            return true;
        }

        private static T Params<T>(object raw)
        {
            try
            {
                var token = raw as JToken ?? JToken.FromObject(raw);
                return token.ToObject<T>() ?? throw new PaymeException(PaymeErrorCodes.InvalidRequest, "Invalid params");
            }
            catch (JsonException)
            {
                throw new PaymeException(PaymeErrorCodes.InvalidRequest, "Invalid params");
            }
        }
    }
}
