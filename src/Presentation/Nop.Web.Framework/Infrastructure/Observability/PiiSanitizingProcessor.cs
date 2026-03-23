using System.Diagnostics;
using OpenTelemetry;

namespace Nop.Web.Framework.Infrastructure.Observability;

/// <summary>
/// A span processor that removes PII from span attributes before export.
///
/// nopCommerce passes rich domain objects (Order, Customer, Address) through its
/// service layer. Any instrumentation that logs method parameters or entity fields
/// risks leaking PII into the tracing backend. Rather than relying on each
/// instrumentation point to manually exclude fields, this processor acts as a
/// safety net at the SDK layer — it runs on every span just before export and
/// strips any attribute whose key matches a known PII pattern.
/// </summary>
public class PiiSanitizingProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> PiiAttributeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        // Customer identity
        "customer.email",
        "customer.name",
        "customer.first_name",
        "customer.last_name",
        "customer.phone",
        "customer.ip",
        "user.email",
        "enduser.id",

        // Payment / card details
        "payment.card_number",
        "payment.card_cvv",
        "payment.card_expiry",
        "payment.cardholder_name",
        "card.number",
        "card.cvv",

        // Address fields
        "address.street",
        "address.line1",
        "address.line2",
        "address.city",
        "address.zip",
        "address.postal_code",
        "address.phone",

        // Generic PII
        "email",
        "phone",
        "ip_address"
    };

    private static readonly string[] PiiSubstrings =
    [
        "email", "card_number", "cvv", "cardholder",
        "phone_number", "street_address", "postal_code"
    ];

    public override void OnEnd(Activity activity)
    {
        if (activity == null)
            return;

        foreach (var tag in activity.Tags)
        {
            if (IsPiiKey(tag.Key))
                activity.SetTag(tag.Key, "[REDACTED]");
        }
    }

    private static bool IsPiiKey(string key)
    {
        if (PiiAttributeKeys.Contains(key))
            return true;

        var lowerKey = key.ToLowerInvariant();
        foreach (var substring in PiiSubstrings)
        {
            if (lowerKey.Contains(substring))
                return true;
        }

        return false;
    }
}
