using System.ComponentModel;

namespace Chapter18.Guardrails;

public sealed class GuardrailFunctions
{
    private readonly string _authorizedUserId;
    private readonly Action<string>? _writeOutput;
    private readonly Action? _onAuthorizedRead;

    public GuardrailFunctions(
        string authorizedUserId,
        Action<string>? writeOutput = null,
        Action? onAuthorizedRead = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedUserId);
        _authorizedUserId = authorizedUserId;
        _writeOutput = writeOutput;
        _onAuthorizedRead = onAuthorizedRead;
    }

    [Description("Read the sample profile only when the requested user ID matches the authenticated user.")]
    public string GetUserProfile(
        [Description("User ID for the requested profile. The function validates this against authenticated context.")]
        string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (!string.Equals(userId, _authorizedUserId, StringComparison.Ordinal))
        {
            _writeOutput?.Invoke("Tool parameter validation failed; protected operation was not executed.");
            return "BLOCKED: User ID validation failed. No profile was read.";
        }

        _writeOutput?.Invoke("Tool parameter validation passed; returning simulated profile data.");
        _onAuthorizedRead?.Invoke();
        return "AUTHORIZED: Sample profile access granted for the authenticated user.";
    }
}
