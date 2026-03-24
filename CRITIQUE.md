# Critique

## What Helped Instrumentation

nopCommerce's strict layered architecture and interface-driven DI made the decorator pattern viable without touching business logic. Every service is registered behind an interface (`IEventPublisher`, `IRepository<T>`, `IOrderProcessingService`), and the `INopStartup` discovery mechanism allowed us to wire observability decorators at startup with zero changes to existing registration code — just a new class with `Order = 2001`.

The `IEventPublisher` was a natural instrumentation boundary. Because every significant state change (order placed, order paid, entity inserted) flows through `PublishAsync<TEvent>()`, wrapping it with a single decorator gave us per-event spans across the entire checkout pipeline without touching any consumer or publisher call site.

The `partial` and `virtual` keywords on all service classes meant we could extend or decorate them freely. The fact that all services are scoped (per-request) made Activity context propagation straightforward — each HTTP request gets its own DI scope, and spans nest correctly within the ambient `Activity`.

## What Hindered Instrumentation

**LINQ2DB has no `DiagnosticSource` integration.** Unlike Entity Framework Core, which emits SQL spans automatically when OpenTelemetry is configured, LINQ2DB is invisible to tracing. This forced us to build the `InstrumentedRepository<T>` decorator to get any database-level visibility at all. Without it, traces would jump from service calls directly to event publishing with no indication of what the database was doing.

**`EngineContext.Current` is a static service locator.** The `EventPublisher` resolves all `IConsumer<TEvent>` implementations through `EngineContext.Current.ResolveAll<>()` rather than constructor injection. This means the decorator pattern works for wrapping `IEventPublisher` itself, but we cannot intercept the resolution of individual consumers — they are resolved outside the DI pipeline. If a consumer is slow or throws, the span on `PublishAsync` captures the total time, but we cannot attribute it to a specific consumer without modifying `EventPublisher` internals.

**`OrderProcessingService` has 41 constructor dependencies.** Writing a full decorator for `IOrderProcessingService` would require implementing ~20 interface methods as pass-throughs, which is tedious and fragile (any interface change breaks the decorator). This is why we opted for surgical `ActivitySource` spans inside `PlaceOrderAsync` instead — fewer lines changed, same observability outcome.

**The custom `ILogger` writes to the database via `IRepository<Log>`, not to `Microsoft.Extensions.Logging`.** This means trace IDs and span IDs are not automatically correlated with application logs. An operator viewing a trace in Jaeger cannot click through to the corresponding log entries. Fixing this would require replacing the logging infrastructure, which is a significant architectural change beyond the scope of instrumentation.

## Surgical Changes and Their Justification

The only existing file we modified in the business logic layer was `OrderProcessingService.cs`. The changes were:

1. **Static fields** (lines 48–61): Added an `ActivitySource`, a `Meter`, and six instrument definitions. These are `static readonly` — they add no per-request overhead and no constructor dependencies.

2. **`PlaceOrderAsync` method**: Wrapped four sub-steps (validate, process payment, save order, move items/inventory) with `using var activity = _activitySource.StartActivity(...)` blocks and `Stopwatch`-based metric recording. The original control flow, error handling, and return values are untouched — every existing line of business logic executes exactly as before.

3. **`MoveShoppingCartItemsToOrderItemsAsync`**: Added one line to record `_itemsPurchased.Add(...)` after each order item is inserted. This records which products are sold for the top-5 dashboard panel.

We minimised impact by using only `System.Diagnostics` types (part of the .NET BCL) — no OpenTelemetry NuGet package was added to `Nop.Services`. The OTel SDK in `Nop.Web.Framework` captures these spans by matching the `ActivitySource` name. If the OTel SDK is removed, the `ActivitySource.StartActivity()` calls return `null` and the instrumentation becomes a no-op with zero runtime cost.

Everything else — the OTel SDK setup, the PII sanitizer, the decorators for `IEventPublisher` and `IRepository<T>`, the Prometheus exporter — lives in new files under `Nop.Web.Framework/Infrastructure/Observability/`. No existing files in that project were modified.
