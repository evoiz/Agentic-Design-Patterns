using System.Text.Json.Serialization;

namespace Guardrails;

[JsonConverter(typeof(GuardrailPolicyVariantConverter))]
public enum GuardrailPolicyVariant
{
    Conservative,
    LegacyNotebook
}

public sealed class GuardrailPolicyVariantConverter() :
    JsonStringEnumConverter<GuardrailPolicyVariant>(allowIntegerValues: false);
