using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Nop.Web.Framework.Infrastructure.Observability;

/// <summary>
/// Centralized OpenTelemetry diagnostics configuration.
/// All ActivitySources and Meters are defined here to ensure consistent naming
/// and to make it easy to register them with the OTel SDK in one place.
/// </summary>
public static class DiagnosticsConfig
{
    public const string ServiceName = "NopCommerce";

    // --- Activity Sources (Tracing) ---

    /// <summary>
    /// Spans for the checkout/order placement pipeline.
    /// Used surgically inside OrderProcessingService.PlaceOrderAsync sub-steps.
    /// </summary>
    public static readonly ActivitySource CheckoutSource = new("NopCommerce.Checkout");

    /// <summary>
    /// Spans for domain event publishing via IEventPublisher.
    /// </summary>
    public static readonly ActivitySource EventsSource = new("NopCommerce.Events");

    /// <summary>
    /// Spans for database operations via IRepository&lt;T&gt;.
    /// </summary>
    public static readonly ActivitySource DataSource = new("NopCommerce.Data");

    // --- Meter & Instruments (Metrics) ---

    public static readonly Meter CheckoutMeter = new("NopCommerce.Checkout");

    /// <summary>
    /// Histogram tracking duration (in milliseconds) of each checkout sub-step.
    /// Tags: step = {validate, process_payment, save_order, move_items_and_inventory}
    ///
    /// Operational justification: if payment step latency doubles at 2 AM, the on-call
    /// engineer immediately knows to check the payment gateway — not the database, not
    /// the cache. This metric breaks the monolithic PlaceOrderAsync into individually
    /// observable segments so degradation is attributable before users see errors.
    /// </summary>
    public static readonly Histogram<double> CheckoutStepDuration =
        CheckoutMeter.CreateHistogram<double>(
            name: "nopcommerce.checkout.step.duration",
            unit: "ms",
            description: "Duration of individual checkout pipeline steps");

    /// <summary>
    /// Counter tracking the outcome of every checkout attempt.
    /// Tags: outcome = {success, payment_failed, validation_failed, error}
    ///
    /// Operational justification: a spike in payment_failed means the payment provider
    /// is degraded — page the payments team. A spike in validation_failed may indicate
    /// stale product/pricing data. An on-call engineer can immediately triage which
    /// upstream system to investigate without reading logs.
    /// </summary>
    public static readonly Counter<long> CheckoutOutcome =
        CheckoutMeter.CreateCounter<long>(
            name: "nopcommerce.checkout.outcome",
            unit: "{outcome}",
            description: "Count of checkout attempts by outcome");
}
