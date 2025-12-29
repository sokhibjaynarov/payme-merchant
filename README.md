# Payme Merchant API for .NET

Unofficial .NET Standard 2.1 NuGet package for integrating Payme (Paycom Uzbekistan) Merchant API.

## Features

- **Strict Protocol Compliance**: Implements JSON-RPC 2.0 and Payme's state machine requirements.
- **Security**: Built-in Basic Authentication validation.
- **Extensible**: Define your own Order Service and Transaction Store.
- **Production Ready**: Robust error handling and clean architecture.

## Installation

Add the `Payme.Merchant` project/package to your solution.

## Configuration

In `Program.cs` (ASP.NET Core 6+):

```csharp
using Payme.Merchant.Extensions;

builder.Services.AddPaymeMerchant(options =>
{
    options.Login = "Paycom"; // Default for Payme
    options.Password = "YOUR_SECRET_KEY"; // Key from Payme Merchant Cabinet
});
```

## Implementing Business Logic

You must implement two interfaces:

### 1. IPaymeOrderService
Handles order validation and status checks.

```csharp
public interface IPaymeOrderService
{
    Task<bool> ExistsAsync(string orderId);
    Task<bool> IsPaidAsync(string orderId);
    Task<long> GetAmountAsync(string orderId); // Amount in Tiyin
    Task MarkAsPaidAsync(string orderId);
    Task CancelAsync(string orderId, int reason);
}
```

### 2. IPaymeTransactionStore
Handles transaction persistence (Idempotency is crucial).

```csharp
public interface IPaymeTransactionStore
{
    Task CreateAsync(string paymeTransactionId, string orderId, long time, long amount);
    Task<PaymeTransaction?> GetAsync(string paymeTransactionId);
    Task PerformAsync(string paymeTransactionId, long performTime);
    Task CancelAsync(string paymeTransactionId, long cancelTime, int reason, int newState);
}
```

Register them in DI:
```csharp
builder.Services.AddScoped<IPaymeOrderService, MyOrderService>();
builder.Services.AddScoped<IPaymeTransactionStore, MyTransactionStore>();
```

## Controller Usage

Create an API endpoint (e.g., `POST /api/payme`):

```csharp
[HttpPost]
public async Task<IActionResult> Post(
    [FromServices] PaymeDispatcher dispatcher)
{
    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync();
    var authHeader = Request.Headers["Authorization"].ToString();

    // Dispatches request and returns JSON-RPC response
    var responseJson = await dispatcher.DispatchAsync(authHeader, body);

    return Content(responseJson, "application/json");
}
```

## Payme Payment Flow

1. **User Initiation**: User clicks "Pay with Payme" -> Redirects to `https://checkout.paycom.uz/...`.
2. **Callbacks**: Payme sends JSON-RPC requests to your `/api/payme` endpoint.
   - `CheckPerformTransaction`: Validates order.
   - `CreateTransaction`: Reserves money/creates transaction.
   - `PerformTransaction`: Confirms payment (Money transferred).
   - `CancelTransaction`: Refunds/Cancels.
   - `CheckTransaction`: Status check.
   
## Security Notes

- **HTTPS Required**: Payme requires SSL.
- **Basic Auth**: The library validates the `Authorization` header against the configured password.
- **Idempotency**: `CreateTransaction` implements idempotency logic (returning existing transaction if checks pass).
