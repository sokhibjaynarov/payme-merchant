# Changelog

All notable changes to this project are documented in this file.

## [2.0.0] — 2026-11-12

First release of the Payme-spec-compliant library (the work previously labeled 0.2.0 on `main`). This is a **breaking** update from the 1.0.0 package on NuGet. After upgrading, existing 1.0.0 integrations will not compile until the steps below are applied.

### Breaking changes

Compared with [Payme.Merchant 1.0.0](https://www.nuget.org/packages/Payme.Merchant/1.0.0):

1. **`IPaymeTransactionStore.CreateAsync` gained a required fifth parameter.**  
   Was: `CreateAsync(string paymeTransactionId, string orderId, long time, long amount)`.  
   Now: `CreateAsync(string paymeTransactionId, string orderId, long paymeTime, long amount, long createTime)`.  
   `paymeTime` is the `time` Payme sent (when the payment started on their side). `createTime` is your server clock at the moment you record the transaction, and is returned to Payme as `create_time`.

2. **`IPaymeTransactionStore.GetByPaymeTimeRangeAsync(long from, long to)` is a new required method** (no default implementation). It must return transactions whose Payme `time` falls in `[from, to]`, and is used by `GetStatement`.

3. **`PaymeTransaction.Reason` changed from `int` to `int?`.** Treat an absent reason as `null`, not `0`.

4. **`PaymeOptions.Key` was removed.** Use `Password` for the merchant key from the Payme cabinet.

5. **`PaymeDispatcher`'s constructor inserted an optional `TimeProvider? timeProvider = null` before the existing optional `logger`.** The old four-argument constructor no longer exists. A positional call such as `new PaymeDispatcher(options, orders, store, logger)` will not compile (`ILogger` is not a `TimeProvider`). Use a named argument: `logger: logger`. `AddPaymeMerchant(...)` is unchanged and needs no call-site update.

### Migration from 1.0.0

1. Update your `IPaymeTransactionStore` implementation:
   - Change `CreateAsync` to take `paymeTime` and `createTime`. Store both: `Time = paymeTime`, `CreateTime = createTime`. Do not substitute one for the other.
   - Implement `GetByPaymeTimeRangeAsync` by filtering on Payme's `time` (not `createTime`).
2. Change any code that reads `PaymeTransaction.Reason` as a non-nullable `int` (for example `int r = tx.Reason`) to handle `int?`.
3. Replace `options.Key = ...` with `options.Password = ...`.
4. If you construct `PaymeDispatcher` by hand and pass a logger, switch to `new PaymeDispatcher(options, orders, store, logger: logger)`. Prefer `AddPaymeMerchant` when using DI.
5. Optionally implement `IPaymeOrderService.CanCancelAfterPerformAsync` (default is `false`, which answers Payme with `-31007`) and `GetDetailAsync` (default is `null`, which omits fiscal `detail`). These have default interface implementations, so existing order services still compile.

### Added

- `GetStatement` Merchant API method.
- Cancel-after-perform: if `CanCancelAfterPerformAsync` returns `true`, a performed transaction is moved to state `-2`; otherwise Payme receives `-31007`.
- Created transactions that are not performed within `PaymeOptions.TransactionTimeout` (default 12 hours) are cancelled with reason `4`.
- `CancelReason` constants, `StatementResult` / `StatementTransaction`, and `PaymeErrorCodes.InvalidRequest` (`-32600`).
- Optional fiscal `detail` via `GetDetailAsync`.
- New dependency: `Microsoft.Bcl.TimeProvider` 8.0.1 (for `netstandard2.1`).

### Changed

- `PaymeErrors.OrderNotFound` messages are now "Order not found" (was "Phone number not found").
- Authentication is checked after the JSON-RPC body is parsed, so a failed auth response can echo the request `id`.

## [1.0.0] — 2026-01-12

Initial package published to NuGet.
