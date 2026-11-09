using System.Text.Json.Serialization;

namespace ResourceOptimization.Models;

[JsonConverter(typeof(OptimizationCategoryConverter))]
public enum OptimizationCategory
{
    Simple,
    Reasoning,
    [JsonStringEnumMemberName("internet_search")]
    InternetSearch
}

public sealed class OptimizationCategoryConverter() : JsonStringEnumConverter<OptimizationCategory>(allowIntegerValues: false);
