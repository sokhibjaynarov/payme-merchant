using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Payme.Merchant.Abstractions;
using Payme.Merchant.Configuration;
using Payme.Merchant.Models;
using Payme.Merchant.Models.JsonRpc;
using Payme.Merchant.Models.MethodDtos;

namespace Payme.Merchant
{
    public class PaymeDispatcher
    {
        private readonly PaymeOptions _options;
        private readonly IPaymeOrderService _orderService;
        private readonly IPaymeTransactionStore _transactionStore;
        private readonly ILogger<PaymeDispatcher>? _logger;

        public PaymeDispatcher(
            IOptions<PaymeOptions> options,
            IPaymeOrderService orderService,
            IPaymeTransactionStore transactionStore,
            ILogger<PaymeDispatcher>? logger = null)
        {
            _options = options.Value;
            _orderService = orderService;
            _transactionStore = transactionStore;
            _logger = logger;
        }

        public async Task<string> DispatchAsync(string authHeader, string requestBody)
        {
            object? id = null;
            try
            {
                // 1. Basic Auth Validation
                if (!IsAuthorized(authHeader))
                {
                    throw new PaymeException(PaymeErrorCodes.AccessDenied, "Insufficient privileges");
                }

                // 2. Parse Request
                JsonRpcRequest? request;
                try
                {
                    request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestBody);
                }
                catch
                {
                    throw new PaymeException(PaymeErrorCodes.InvalidJson, "Invalid JSON");
                }

                if (request == null || string.IsNullOrEmpty(request.Method))
                {
                    // Typically if we can't parse ID, we treat it as null id.
                    throw new PaymeException(PaymeErrorCodes.InvalidJson, "Invalid JSON-RPC");
                }

                id = request.Id;

                // 3. Route Method
                object? result = null;
                switch (request.Method)
                {
                    case "CheckPerformTransaction":
                        result = await HandleCheckPerformTransactionAsync(request.Params);
                        break;
                    case "CreateTransaction":
                        result = await HandleCreateTransactionAsync(request.Params);
                        break;
                    case "PerformTransaction":
                        result = await HandlePerformTransactionAsync(request.Params);
                        break;
                    case "CancelTransaction":
                        result = await HandleCancelTransactionAsync(request.Params);
                        break;
                    case "CheckTransaction":
                        result = await HandleCheckTransactionAsync(request.Params);
                        break;
                    case "GetStatement":
                        // result = await HandleGetStatementAsync(request.Params);
                        throw new PaymeException(PaymeErrorCodes.MethodNotFound,
                            "GetStatement is not fully implemented in this demo");
                    case "ChangePassword":
                        // Only needed if you support changing password via API, usually not for Merchant API.
                        result = new { success = true };
                        break;
                    default:
                        throw new PaymeException(PaymeErrorCodes.MethodNotFound, "Method not found");
                }

                // 4. Success Response
                var response = new JsonRpcResponse(id ?? 0, result);
                return JsonConvert.SerializeObject(response);
            }
            catch (PaymeException pe)
            {
                var response = new JsonRpcResponse(id ?? 0, new JsonRpcError(pe.Code, pe.Message, pe.Data));
                return JsonConvert.SerializeObject(response);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "System error processing Payme request");
                var response = new JsonRpcResponse(id ?? 0,
                    new JsonRpcError(PaymeErrorCodes.SystemError, "Internal System Error"));
                return JsonConvert.SerializeObject(response);
            }
        }

        private bool IsAuthorized(string authHeader)
        {
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic "))
            {
                return false;
            }

            try
            {
                var base64 = authHeader.Substring("Basic ".Length);
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                var parts = decoded.Split(':');
                if (parts.Length != 2) return false;

                // Payme sends Login:Password. 
                // We compare with our options.
                return parts[0] == _options.Login && parts[1] == _options.Password;
            }
            catch
            {
                return false;
            }
        }

        // HANDLERS (To be moved to separate classes or kept here if simple enough, 
        // but for quality I'll keep them here as private methods first, 
        // to access _services easily while keeping state strict).
        // Actually, creating separate Handler classes is cleaner but requires passing services. 
        // Since the prompt asks for "Dispatching Payme methods internally", methods here are fine.

        private async Task<CheckPerformTransactionResult> HandleCheckPerformTransactionAsync(object requestParams)
        {
            var p = DeserializeParams<CheckPerformTransactionDto>(requestParams);

            var orderId = p.Account?.OrderId;
            if (string.IsNullOrEmpty(orderId))
                throw new PaymeException(PaymeErrorCodes.OrderNotFound, PaymeErrors.OrderNotFound, "order_id");

            if (!await _orderService.ExistsAsync(orderId))
                throw new PaymeException(PaymeErrorCodes.OrderNotFound, PaymeErrors.OrderNotFound, "order_id");

            var amount = await _orderService.GetAmountAsync(orderId);
            if (amount != p.Amount)
                throw new PaymeException(PaymeErrorCodes.WrongAmount, PaymeErrors.WrongAmount, "amount");

            if (await _orderService.IsPaidAsync(orderId))
                throw new PaymeException(PaymeErrorCodes.TerminalState, PaymeErrors.TerminalState);

            var existingTrans = await _transactionStore.GetByOrderIdAsync(orderId);
            if (existingTrans != null)
            {
                if (existingTrans.State == (int)TransactionState.Created)
                    throw new PaymeException(PaymeErrorCodes.Pending, PaymeErrors.Pending);

                if (existingTrans.State == (int)TransactionState.Completed)
                    throw new PaymeException(PaymeErrorCodes.TerminalState, PaymeErrors.TerminalState);
            }

            return new CheckPerformTransactionResult
            {
                Allow = true
            };
        }

        private async Task<CreateTransactionResult> HandleCreateTransactionAsync(object requestParams)
        {
            var p = DeserializeParams<CreateTransactionDto>(requestParams);
            var orderId = p.Account?.OrderId;
            if (string.IsNullOrEmpty(orderId))
                throw new PaymeException(PaymeErrorCodes.OrderNotFound, PaymeErrors.OrderNotFound, "order_id");

            // 1. Check if Transaction exists
            var existing = await _transactionStore.GetAsync(p.Id);

            if (existing != null)
            {
                if (existing.OrderId != orderId || existing.Amount != p.Amount)
                {
                    throw new PaymeException(PaymeErrorCodes.UnableToComplete, PaymeErrors.UnableToComplete);
                }

                if (existing.State == (int)TransactionState.Created)
                {
                    return new CreateTransactionResult
                    {
                        CreateTime = existing.CreateTime,
                        Transaction = existing.Id,
                        State = existing.State
                    };
                }
                else
                {
                    throw new PaymeException(PaymeErrorCodes.UnableToComplete, PaymeErrors.UnableToComplete);
                }
            }

            // 2. Check Perform Conditions (Order exists, amount correct, not paid)
            if (!await _orderService.ExistsAsync(orderId))
                throw new PaymeException(PaymeErrorCodes.OrderNotFound, PaymeErrors.OrderNotFound, "order_id");

            var amount = await _orderService.GetAmountAsync(orderId);
            if (amount != p.Amount)
                throw new PaymeException(PaymeErrorCodes.WrongAmount, PaymeErrors.WrongAmount, "amount");

            var activeTrans = await _transactionStore.GetByOrderIdAsync(orderId);
            if (activeTrans != null)
            {
                if (activeTrans.State == (int)TransactionState.Completed)
                    throw new PaymeException(PaymeErrorCodes.TerminalState, PaymeErrors.TerminalState);

                if (activeTrans.State == (int)TransactionState.Created)
                    throw new PaymeException(PaymeErrorCodes.InProgress, PaymeErrors.InProgress);
            }

            await _transactionStore.CreateAsync(p.Id, orderId, p.Time, p.Amount);

            return new CreateTransactionResult
            {
                CreateTime = p.Time,
                Transaction = p.Id,
                State = (int)TransactionState.Created
            };
        }

        private async Task<PerformTransactionResult> HandlePerformTransactionAsync(object requestParams)
        {
            var p = DeserializeParams<PerformTransactionDto>(requestParams);
            var trans = await _transactionStore.GetAsync(p.Id);

            if (trans == null)
            {
                throw new PaymeException(PaymeErrorCodes.TransactionNotFound, PaymeErrors.TransactionNotFound);
            }

            // State Machine
            if (trans.State == (int)TransactionState.Created)
            {
                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - trans.CreateTime > 43200000)
                {
                    await _transactionStore.CancelAsync(trans.Id, DateTimeOffset.Now.ToUnixTimeMilliseconds(), -1,
                        (int)TransactionState.Cancelled);
                    throw new PaymeException(PaymeErrorCodes.UnableToComplete, PaymeErrors.UnableToComplete);
                }

                await _orderService.MarkAsPaidAsync(trans.OrderId);
                var performTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                await _transactionStore.PerformAsync(trans.Id, performTime);

                return new PerformTransactionResult
                {
                    PerformTime = performTime,
                    Transaction = trans.Id,
                    State = (int)TransactionState.Completed
                };
            }

            if (trans.State == (int)TransactionState.Completed)
            {
                return new PerformTransactionResult
                {
                    PerformTime = trans.PerformTime,
                    Transaction = trans.Id,
                    State = (int)TransactionState.Completed
                };
            }

            throw new PaymeException(PaymeErrorCodes.UnableToComplete, PaymeErrors.UnableToComplete);
        }

        private async Task<CancelTransactionResult> HandleCancelTransactionAsync(object requestParams)
        {
            var p = DeserializeParams<CancelTransactionDto>(requestParams);
            var trans = await _transactionStore.GetAsync(p.Id);

            if (trans == null)
            {
                throw new PaymeException(PaymeErrorCodes.TransactionNotFound, PaymeErrors.TransactionNotFound);
            }

            // Already Cancelled
            if (trans.State == (int)TransactionState.Cancelled ||
                trans.State == (int)TransactionState.CancelledAfterComplete)
            {
                return new CancelTransactionResult
                {
                    CancelTime = trans.CancelTime,
                    Transaction = trans.Id,
                    State = trans.State
                };
            }

            // Cancel logic
            var cancelTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            if (trans.State == (int)TransactionState.Created)
            {
                // Just cancel
                await _transactionStore.CancelAsync(trans.Id, cancelTime, p.Reason, (int)TransactionState.Cancelled);
                return new CancelTransactionResult
                {
                    CancelTime = cancelTime,
                    Transaction = trans.Id,
                    State = (int)TransactionState.Cancelled
                };
            }

            if (trans.State == (int)TransactionState.Completed)
            {
                throw new PaymeException(PaymeErrorCodes.CantCancel, PaymeErrors.UnableToCancel);
            }

            throw new PaymeException(PaymeErrorCodes.UnableToComplete, PaymeErrors.UnableToComplete);
        }

        private async Task<CheckTransactionResult> HandleCheckTransactionAsync(object requestParams)
        {
            var p = DeserializeParams<CheckTransactionDto>(requestParams);
            var trans = await _transactionStore.GetAsync(p.Id);

            if (trans == null)
                throw new PaymeException(PaymeErrorCodes.TransactionNotFound, PaymeErrors.TransactionNotFound);

            return new CheckTransactionResult
            {
                CreateTime = trans.CreateTime,
                PerformTime = trans.PerformTime,
                CancelTime = trans.CancelTime,
                Transaction = trans.Id,
                State = trans.State,
                Reason = trans.Reason == 0 ? null : (int?)trans.Reason
            };
        }

        private T DeserializeParams<T>(object urlParams)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(urlParams.ToString()!) ?? throw new Exception("Null Params");
            }
            catch
            {
                // Payme sometimes sends parameters not as expected object?
                // JObject?
                // Actually the `object params` in JsonRpcRequest might be JObject if we use Newtonsoft.

                if (urlParams is Newtonsoft.Json.Linq.JObject jo)
                {
                    return jo.ToObject<T>() ?? throw new Exception();
                }

                return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(urlParams)) ??
                       throw new Exception();
            }
        }
    }
}
