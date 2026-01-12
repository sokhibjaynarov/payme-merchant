# Payme Merchant API for .NET

<p align="center">
  <strong>Unofficial .NET Standard 2.1 library for integrating Payme (Paycom Uzbekistan) Merchant API</strong>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#installation">Installation</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#api-reference">API Reference</a> •
  <a href="#examples">Examples</a>
</p>

---

## Overview

This library provides a complete implementation of the [Payme Merchant API](https://developer.help.paycom.uz/metody-merchant-api/) for .NET applications. It handles all the complexity of JSON-RPC 2.0 communication, transaction state management, and security validation, allowing you to focus on your business logic.

## Features

- ✅ **Full JSON-RPC 2.0 Compliance** — Strict adherence to the specification with proper error handling
- 🔐 **Built-in Security** — Basic Authentication validation out of the box
- 🌐 **Multilingual Error Messages** — Supports Russian, Uzbek, and English error responses
- 🔄 **Idempotency Support** — Proper handling of duplicate transaction requests
- ⚙️ **Dependency Injection** — ASP.NET Core DI integration via extension methods
- 📦 **Clean Architecture** — Separation of concerns with interfaces for business logic

## Installation

### Option 1: Add Project Reference

Add the `Payme.Merchant` project to your solution and reference it.

### Option 2: NuGet Package (Coming Soon)

```bash
dotnet add package Payme.Merchant
```

## Quick Start

### 1. Register Services

In your `Program.cs` (ASP.NET Core 6+):

```csharp
using Payme.Merchant.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register Payme Merchant with your credentials
builder.Services.AddPaymeMerchant(options =>
{
    options.Login = "Paycom";                    // Default login
    options.Password = "YOUR_SECRET_KEY";        // From Payme Merchant Cabinet
});

// Register your implementations of the required interfaces
builder.Services.AddScoped<IPaymeOrderService, YourOrderService>();
builder.Services.AddScoped<IPaymeTransactionStore, YourTransactionStore>();
```

### 2. Implement Required Interfaces

You must implement two interfaces to connect the library to your business logic:

#### IPaymeOrderService

Handles order validation and status management:

```csharp
public interface IPaymeOrderService
{
    /// <summary>
    /// Checks if an order with the given ID exists.
    /// </summary>
    Task<bool> ExistsAsync(string orderId);

    /// <summary>
    /// Checks if the order has already been paid.
    /// </summary>
    Task<bool> IsPaidAsync(string orderId);

    /// <summary>
    /// Gets the order amount in tiyin (1 UZS = 100 tiyin).
    /// </summary>
    Task<long> GetAmountAsync(string orderId);

    /// <summary>
    /// Marks the order as paid after successful transaction.
    /// </summary>
    Task MarkAsPaidAsync(string orderId);

    /// <summary>
    /// Cancels/refunds the order.
    /// </summary>
    /// <param name="orderId">The order ID</param>
    /// <param name="reason">Cancellation reason code from Payme</param>
    Task CancelAsync(string orderId, int reason);
}
```

#### IPaymeTransactionStore

Handles transaction persistence (critical for idempotency):

```csharp
public interface IPaymeTransactionStore
{
    /// <summary>
    /// Creates a new transaction record.
    /// </summary>
    /// <param name="paymeTransactionId">Unique ID from Payme</param>
    /// <param name="orderId">Your order ID</param>
    /// <param name="time">Transaction creation time (Unix ms)</param>
    /// <param name="amount">Amount in tiyin</param>
    Task CreateAsync(string paymeTransactionId, string orderId, long time, long amount);

    /// <summary>
    /// Retrieves a transaction by its Payme ID.
    /// </summary>
    Task<PaymeTransaction?> GetAsync(string paymeTransactionId);

    /// <summary>
    /// Retrieves a transaction by order ID.
    /// </summary>
    /// <remarks>
    /// Used for idempotency checks when multiple transactions target the same order.
    /// </remarks>
    Task<PaymeTransaction?> GetByOrderIdAsync(string orderId);

    /// <summary>
    /// Marks a transaction as performed/completed.
    /// </summary>
    Task PerformAsync(string paymeTransactionId, long performTime);

    /// <summary>
    /// Cancels a transaction.
    /// </summary>
    /// <param name="paymeTransactionId">The Payme transaction ID</param>
    /// <param name="cancelTime">Cancellation timestamp (Unix ms)</param>
    /// <param name="reason">Cancellation reason code</param>
    /// <param name="newState">New state (-1 for Cancelled, -2 for CancelledAfterComplete)</param>
    Task CancelAsync(string paymeTransactionId, long cancelTime, int reason, int newState);
}
```

### 3. Create Controller Endpoint

Create an API endpoint to receive Payme callbacks:

```csharp
[Route("api/[controller]")]
[ApiController]
public class PaymeController : ControllerBase
{
    private readonly PaymeDispatcher _dispatcher;

    public PaymeController(PaymeDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        var authHeader = Request.Headers["Authorization"].ToString();

        // Dispatches request and returns JSON-RPC response
        var responseJson = await _dispatcher.DispatchAsync(authHeader, body);

        return Content(responseJson, "application/json");
    }
}
```

---

## API Reference

### Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Login` | `string` | `"Paycom"` | Payme merchant login |
| `Password` | `string` | - | Secret key from Payme Merchant Cabinet |

### Transaction States

The library uses strict state management as per Payme specification:

| State | Value | Description |
|-------|-------|-------------|
| `Created` | `1` | Transaction created, awaiting payment confirmation |
| `Completed` | `2` | Payment successfully completed |
| `Cancelled` | `-1` | Transaction cancelled before completion |
| `CancelledAfterComplete` | `-2` | Transaction refunded after completion |

```csharp
public enum TransactionState
{
    Created = 1,
    Completed = 2,
    Cancelled = -1,
    CancelledAfterComplete = -2
}
```

### PaymeTransaction Model

The transaction model used by `IPaymeTransactionStore`:

```csharp
public class PaymeTransaction
{
    public string Id { get; set; }          // Payme transaction ID
    public string OrderId { get; set; }     // Your order ID
    public long Time { get; set; }          // Request time (Unix ms)
    public long Amount { get; set; }        // Amount in tiyin
    public int State { get; set; }          // TransactionState
    public long CreateTime { get; set; }    // Creation timestamp (Unix ms)
    public long PerformTime { get; set; }   // Completion timestamp (Unix ms)
    public long CancelTime { get; set; }    // Cancellation timestamp (Unix ms)
    public int Reason { get; set; }         // Cancellation reason code
}
```

### Supported Methods

The `PaymeDispatcher` handles all standard Payme Merchant API methods:

| Method | Description |
|--------|-------------|
| `CheckPerformTransaction` | Validates order before payment |
| `CreateTransaction` | Creates a new payment transaction |
| `PerformTransaction` | Confirms and completes payment |
| `CancelTransaction` | Cancels/refunds a transaction |
| `CheckTransaction` | Checks current transaction status |
| `GetStatement` | Returns transaction history (not fully implemented) |
| `ChangePassword` | Updates merchant password |

---

## Error Handling

### Error Codes

The library provides predefined error codes matching Payme specification:

| Code | Constant | Description |
|------|----------|-------------|
| `-32700` | `InvalidJson` | Parse error |
| `-32601` | `MethodNotFound` | Method not found |
| `-32504` | `AccessDenied` | Authentication failed |
| `-32400` | `SystemError` | Internal server error |
| `-32300` | `TransportError` | Transport level error |
| `-31001` | `WrongAmount` | Incorrect amount |
| `-31003` | `TransactionNotFound` | Transaction not found |
| `-31007` | `CantCancel` | Cannot cancel transaction |
| `-31008` | `UnableToComplete` | Cannot complete operation |
| `-31050` | `OrderNotFound` | Order not found |
| `-31088` | `InProgress` | Transaction in progress |
| `-31098` | `TerminalState` | Transaction already completed |
| `-31099` | `Pending` | Temporary error, try later |

### Multilingual Error Messages

All error messages are returned in three languages:

```csharp
public static readonly PaymeErrorMessage OrderNotFound = new PaymeErrorMessage(
    ru: "Номер телефона не найден",
    uz: "Raqam ro'yhatda yo'q",
    en: "Phone number not found"
);
```

### Throwing Custom Errors

Use `PaymeException` to throw errors with proper JSON-RPC formatting:

```csharp
// With predefined multilingual message
throw new PaymeException(
    PaymeErrorCodes.OrderNotFound, 
    PaymeErrors.OrderNotFound, 
    "order_id"  // Optional data field
);

// With simple string message
throw new PaymeException(
    PaymeErrorCodes.WrongAmount, 
    "The amount does not match"
);
```

---

## Payment Flow

Here's how a typical payment flow works:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           PAYMENT FLOW                                   │
└─────────────────────────────────────────────────────────────────────────┘

1. User clicks "Pay with Payme" on your site
          │
          ▼
2. Redirect to: https://checkout.paycom.uz/{ENCODED_PARAMS}
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    PAYME SENDS CALLBACKS TO YOUR API                     │
└─────────────────────────────────────────────────────────────────────────┘
          │
          ├──► CheckPerformTransaction
          │         • Validates order exists
          │         • Verifies amount matches
          │         • Checks order not already paid
          │         • Returns { allow: true } if valid
          │
          ├──► CreateTransaction
          │         • Creates transaction in state=1 (Created)
          │         • Reserves money (not yet transferred)
          │         • Handles idempotency (returns existing if same ID)
          │
          ├──► PerformTransaction
          │         • Moves to state=2 (Completed)
          │         • Money transferred to merchant
          │         • Calls MarkAsPaidAsync on your order
          │
          └──► CancelTransaction (if needed)
                    • Moves to state=-1 (Cancelled) or -2 (CancelledAfterComplete)
                    • Initiates refund
                    • Calls CancelAsync on your order
```

### Transaction Timeout

Transactions in `Created` state have a 12-hour timeout. If `PerformTransaction` is not called within this window, the library automatically cancels the transaction.

---

## Examples

### In-Memory Implementation (for Testing)

See the complete example in `Payme.Merchant.Example/Services/InMemoryPaymeService.cs`:

```csharp
public class InMemoryPaymeService : IPaymeOrderService, IPaymeTransactionStore
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();
    private readonly ConcurrentDictionary<string, PaymeTransaction> _transactions = new();

    public InMemoryPaymeService()
    {
        // Seed test orders
        _orders.TryAdd("1001", new Order { Id = "1001", Amount = 100000, IsPaid = false });
        _orders.TryAdd("1002", new Order { Id = "1002", Amount = 500000, IsPaid = false });
    }

    // IPaymeOrderService implementation
    public Task<bool> ExistsAsync(string orderId) 
        => Task.FromResult(_orders.ContainsKey(orderId));

    public Task<long> GetAmountAsync(string orderId)
    {
        if (_orders.TryGetValue(orderId, out var order))
            return Task.FromResult(order.Amount);
        return Task.FromResult(0L);
    }

    public Task<bool> IsPaidAsync(string orderId)
    {
        if (_orders.TryGetValue(orderId, out var order))
            return Task.FromResult(order.IsPaid);
        return Task.FromResult(false);
    }

    public Task MarkAsPaidAsync(string orderId)
    {
        if (_orders.TryGetValue(orderId, out var order))
            order.IsPaid = true;
        return Task.CompletedTask;
    }

    public Task CancelAsync(string orderId, int reason)
    {
        if (_orders.TryGetValue(orderId, out var order))
            order.IsPaid = false;
        return Task.CompletedTask;
    }

    // IPaymeTransactionStore implementation
    public Task CreateAsync(string paymeTransactionId, string orderId, long time, long amount)
    {
        var transaction = new PaymeTransaction
        {
            Id = paymeTransactionId,
            OrderId = orderId,
            Time = time,
            Amount = amount,
            CreateTime = time,
            State = (int)TransactionState.Created
        };
        _transactions.TryAdd(paymeTransactionId, transaction);
        return Task.CompletedTask;
    }

    public Task<PaymeTransaction?> GetAsync(string paymeTransactionId)
    {
        _transactions.TryGetValue(paymeTransactionId, out var t);
        return Task.FromResult(t);
    }

    public Task<PaymeTransaction?> GetByOrderIdAsync(string orderId)
    {
        var t = _transactions.Values.FirstOrDefault(x => x.OrderId == orderId);
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
}
```

### Entity Framework Implementation

```csharp
public class EfPaymeTransactionStore : IPaymeTransactionStore
{
    private readonly AppDbContext _context;

    public EfPaymeTransactionStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(string paymeTransactionId, string orderId, long time, long amount)
    {
        var transaction = new PaymeTransactionEntity
        {
            Id = paymeTransactionId,
            OrderId = orderId,
            Time = time,
            Amount = amount,
            CreateTime = time,
            State = (int)TransactionState.Created
        };
        
        _context.PaymeTransactions.Add(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task<PaymeTransaction?> GetAsync(string paymeTransactionId)
    {
        var entity = await _context.PaymeTransactions
            .FirstOrDefaultAsync(t => t.Id == paymeTransactionId);
        
        return entity == null ? null : MapToPaymeTransaction(entity);
    }

    public async Task<PaymeTransaction?> GetByOrderIdAsync(string orderId)
    {
        var entity = await _context.PaymeTransactions
            .FirstOrDefaultAsync(t => t.OrderId == orderId);
        
        return entity == null ? null : MapToPaymeTransaction(entity);
    }

    public async Task PerformAsync(string paymeTransactionId, long performTime)
    {
        var entity = await _context.PaymeTransactions
            .FirstOrDefaultAsync(t => t.Id == paymeTransactionId);
        
        if (entity != null)
        {
            entity.State = (int)TransactionState.Completed;
            entity.PerformTime = performTime;
            await _context.SaveChangesAsync();
        }
    }

    public async Task CancelAsync(string paymeTransactionId, long cancelTime, int reason, int newState)
    {
        var entity = await _context.PaymeTransactions
            .FirstOrDefaultAsync(t => t.Id == paymeTransactionId);
        
        if (entity != null)
        {
            entity.State = newState;
            entity.CancelTime = cancelTime;
            entity.Reason = reason;
            await _context.SaveChangesAsync();
        }
    }

    private static PaymeTransaction MapToPaymeTransaction(PaymeTransactionEntity entity)
        => new()
        {
            Id = entity.Id,
            OrderId = entity.OrderId,
            Time = entity.Time,
            Amount = entity.Amount,
            State = entity.State,
            CreateTime = entity.CreateTime,
            PerformTime = entity.PerformTime,
            CancelTime = entity.CancelTime,
            Reason = entity.Reason
        };
}
```

---

## Security Notes

> [!CAUTION]
> **HTTPS is Required** — Payme will only send callbacks to HTTPS endpoints. Ensure your endpoint has a valid SSL certificate.

> [!WARNING]
> **Store Your Secret Key Securely** — Never commit your Payme secret key to version control. Use environment variables or a secrets manager.

> [!IMPORTANT]
> **Basic Authentication** — The library automatically validates the `Authorization` header against your configured credentials. Invalid credentials result in a `-32504 Access Denied` error.

### Best Practices

1. **Use environment variables** for the secret key:
   ```csharp
   builder.Services.AddPaymeMerchant(options =>
   {
       options.Password = builder.Configuration["Payme:SecretKey"];
   });
   ```

2. **Implement proper logging** — The `PaymeDispatcher` accepts an optional `ILogger<PaymeDispatcher>` for logging errors.

3. **Handle idempotency correctly** — Always return the same response for duplicate `CreateTransaction` requests with the same ID.

4. **Test with Payme sandbox** before going to production.

---

## Project Structure

```
Payme.Merchant/
├── Abstractions/
│   └── PaymeInterfaces.cs         # IPaymeOrderService, IPaymeTransactionStore, PaymeTransaction
├── Configuration/
│   └── PaymeOptions.cs            # Configuration options
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # DI registration
├── Models/
│   ├── JsonRpc/
│   │   ├── JsonRpcRequest.cs      # JSON-RPC request model
│   │   ├── JsonRpcResponse.cs     # JSON-RPC response model
│   │   └── JsonRpcError.cs        # JSON-RPC error model
│   ├── MethodDtos/
│   │   ├── MethodDtos.cs          # Request DTOs for each method
│   │   └── MethodResults.cs       # Response DTOs for each method
│   ├── PaymeAccount.cs            # Order account info (order_id)
│   ├── PaymeErrorCodes.cs         # Error code constants
│   ├── PaymeErrorMessage.cs       # Multilingual error message
│   ├── PaymeErrors.cs             # Predefined error messages
│   ├── PaymeException.cs          # Custom exception for Payme errors
│   └── TransactionState.cs        # Transaction state enum
└── PaymeDispatcher.cs             # Main request dispatcher

Payme.Merchant.Example/
├── Controllers/
│   └── PaymeController.cs         # Example API endpoint
├── Services/
│   └── InMemoryPaymeService.cs    # Example in-memory implementation
└── Program.cs                     # Example app configuration
```

---

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License.

## Disclaimer

This is an **unofficial** library and is not affiliated with Paycom/Payme. Use at your own risk. Always test thoroughly in a sandbox environment before deploying to production.
