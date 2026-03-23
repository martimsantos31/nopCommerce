using System.Diagnostics;
using Nop.Core.Events;
using OpenTelemetry.Trace;

namespace Nop.Web.Framework.Infrastructure.Observability;

/// <summary>
/// Decorator that wraps <see cref="IEventPublisher"/> with OpenTelemetry spans.
///
/// nopCommerce's event system (IEventPublisher / IConsumer&lt;T&gt;) is a natural
/// instrumentation boundary: every significant state change in the checkout flow
/// (OrderPlacedEvent, OrderPaidEvent, EntityInsertedEvent&lt;Order&gt;, etc.) passes
/// through PublishAsync. Wrapping it here gives us per-event spans in the trace
/// without touching any business logic.
///
/// The original EventPublisher is stateless (no constructor deps — it resolves
/// consumers via EngineContext.Current), so this decorator simply creates a new
/// instance internally.
/// </summary>
public class InstrumentedEventPublisher : IEventPublisher
{
    private readonly IEventPublisher _inner;

    public InstrumentedEventPublisher(IEventPublisher inner)
    {
        _inner = inner;
    }

    public async Task PublishAsync<TEvent>(TEvent @event)
    {
        var eventTypeName = typeof(TEvent).Name;

        using var activity = DiagnosticsConfig.EventsSource.StartActivity(
            $"Event {eventTypeName}",
            ActivityKind.Internal);

        activity?.SetTag("event.type", eventTypeName);

        try
        {
            await _inner.PublishAsync(@event);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
