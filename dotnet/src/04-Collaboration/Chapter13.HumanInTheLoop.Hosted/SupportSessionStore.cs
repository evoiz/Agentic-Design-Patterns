using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Chapter13.HumanInTheLoop.Hosted;

internal sealed record PendingApproval(
    string RequestId,
    string CallId,
    string Name,
    Dictionary<string, JsonElement> Arguments)
{
    public static PendingApproval From(ToolApprovalRequestContent request) =>
        request.ToolCall is FunctionCallContent call
            ? new PendingApproval(
                request.RequestId,
                call.CallId,
                call.Name,
                (call.Arguments ?? new Dictionary<string, object?>()).ToDictionary(
                    pair => pair.Key,
                    pair => JsonSerializer.SerializeToElement(pair.Value, SupportSessionStore.JsonOptions),
                    StringComparer.Ordinal))
            : throw new InvalidOperationException("The support agent requested approval for an unsupported tool call.");

    // Rebuilds the original approval request so the restored agent session can match the decision to its call.
    public ToolApprovalRequestContent ToRequest() =>
        new(RequestId, new FunctionCallContent(
            CallId,
            Name,
            Arguments.ToDictionary(pair => pair.Key, pair => ConvertJsonValue(pair.Value), StringComparer.Ordinal)));

    private static object? ConvertJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(property => property.Name, property => ConvertJsonValue(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToArray(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => throw new InvalidOperationException("The pending tool arguments contain an unsupported JSON value.")
    };
}

internal sealed record SupportInvocationSession(
    string SessionId,
    string? OwnerId,
    string Status,
    string Task,
    SupportCustomer Customer,
    JsonElement AgentSession,
    List<PendingApproval>? PendingApprovals,
    string? AssistantMessage,
    string? FinalOutput)
{
    public const string AwaitingApproval = "awaiting_approval";
    public const string Completed = "completed";
}

// Durable, owner-partitioned session files under the user profile; writes are atomic (temp file + move).
internal static class SupportSessionStore
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string StateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".chapter13-human-in-the-loop");
    private static readonly Dictionary<string, SemaphoreSlim> Locks = new(StringComparer.Ordinal);

    public static SemaphoreSlim GetLock(string sessionId, string? ownerId)
    {
        lock (Locks)
        {
            var key = GetPartitionKey(sessionId, ownerId);
            if (!Locks.TryGetValue(key, out var sessionLock))
            {
                Locks.Add(key, sessionLock = new SemaphoreSlim(1, 1));
            }

            return sessionLock;
        }
    }

    public static SupportInvocationSession? Load(string sessionId, string? ownerId)
    {
        var path = GetPath(sessionId, ownerId);
        return !File.Exists(path)
            ? null
            : JsonSerializer.Deserialize<SupportInvocationSession>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidOperationException($"Could not deserialize support session '{sessionId}'.");
    }

    public static void Save(SupportInvocationSession session)
    {
        Directory.CreateDirectory(StateDirectory);
        var path = GetPath(session.SessionId, session.OwnerId);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(session, JsonOptions));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private static string GetPartitionKey(string sessionId, string? ownerId) => $"{ownerId ?? string.Empty}\0{sessionId}";

    private static string GetPath(string sessionId, string? ownerId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(GetPartitionKey(sessionId, ownerId))));
        return Path.Combine(StateDirectory, $"session-{hash.ToLowerInvariant()}.json");
    }
}
