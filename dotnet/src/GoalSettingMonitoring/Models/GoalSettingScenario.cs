using System.Text.Json.Serialization;

namespace GoalSettingMonitoring.Models;

[JsonConverter(typeof(GoalSettingScenarioConverter))]
public enum GoalSettingScenario
{
    DotNetDeliverable,
    PythonCode
}

public sealed class GoalSettingScenarioConverter() :
    JsonStringEnumConverter<GoalSettingScenario>(allowIntegerValues: false);
