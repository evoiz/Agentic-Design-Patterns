using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ConferenceDemos;

public sealed record Recipe(string Id, string Name, string[] Preferences, string[] Ingredients);
public sealed record ShoppingRequest(string Preference, IReadOnlyDictionary<string, bool?> Pantry);
public sealed record MealChoice(string RecipeId);
public sealed record ShoppingResult(bool Passed, string Status, string? Meal, IReadOnlyList<string> Missing);

public static class ShoppingDemo
{
    public static IReadOnlyList<Recipe> Recipes { get; } =
    [
        new("chickpea-salad", "Chickpea salad", ["vegan", "vegetarian"], ["chickpeas", "tomato", "cucumber", "olive oil"]),
        new("cheese-pasta", "Cheese pasta", ["vegetarian"], ["pasta", "cheese", "tomato", "olive oil"]),
        new("chicken-rice", "Chicken and rice", ["omnivore"], ["chicken", "rice", "tomato", "olive oil"])
    ];

    public static ShoppingRequest Sample(string preference = "vegetarian") => new(preference,
        new Dictionary<string, bool?>
        {
            ["chickpeas"] = true, ["tomato"] = false, ["cucumber"] = false, ["olive oil"] = true,
            ["pasta"] = true, ["cheese"] = false, ["chicken"] = false, ["rice"] = true
        });

    public static async Task<ShoppingResult> RunAsync(
        IChatClient client, ShoppingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Preference);
        var choose = new ChooseMeal(client);
        var pantry = new CheckPantry();
        var goal = new CheckGoal();
        var workflow = new WorkflowBuilder(choose).AddEdge(choose, pantry).AddEdge(pantry, goal)
            .WithOutputFrom(goal).Build();
        return await ConferenceWorkflow.RunAsync<ShoppingRequest, ShoppingResult>(workflow, request, cancellationToken);
    }

    public static ShoppingResult Validate(ShoppingRequest request, Recipe? recipe, IReadOnlyList<string> missing)
    {
        if (recipe is null || !Recipes.Contains(recipe) || !recipe.Preferences.Contains(request.Preference, StringComparer.OrdinalIgnoreCase))
        {
            return new(false, "Goal check FAIL: no supported meal satisfies the preference.", null, []);
        }
        if (recipe.Ingredients.Any(i => !request.Pantry.TryGetValue(i, out var present) || present is null))
        {
            return new(false, "Goal check FAIL: pantry data is incomplete; unknown is not the same as missing.", recipe.Name, []);
        }
        var expected = recipe.Ingredients.Where(i => request.Pantry[i] == false).ToHashSet(StringComparer.Ordinal);
        if (missing.Distinct(StringComparer.Ordinal).Count() != missing.Count || !expected.SetEquals(missing))
        {
            return new(false, "Goal check FAIL: ingredient accounting does not match the pantry.", recipe.Name, []);
        }
        return new(true, "Goal check PASS: preference met; all ingredients accounted for.", recipe.Name, missing);
    }

    private sealed record SelectedMeal(ShoppingRequest Request, Recipe? Recipe);
    private sealed record PantryCheck(ShoppingRequest Request, Recipe? Recipe, IReadOnlyList<string> Missing);

    private sealed class ChooseMeal(IChatClient client) : Executor<ShoppingRequest, SelectedMeal>("ChooseMeal")
    {
        public override async ValueTask<SelectedMeal> HandleAsync(
            ShoppingRequest request, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            var candidates = Recipes.Where(r => r.Preferences.Contains(request.Preference, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length == 0)
            {
                return new(request, null);
            }
            var planner = new ChatClientAgent(client, name: "DinnerPlanner",
                instructions: "Choose one simple dinner from the supplied candidate recipes. Return its exact RecipeId.");
            var choice = await planner.RunAsync<MealChoice>(
                JsonSerializer.Serialize(new { request.Preference, Recipes = candidates }), cancellationToken: cancellationToken);
            var recipe = candidates.SingleOrDefault(r => r.Id == choice.Result.RecipeId);
            return new(request, recipe);
        }
    }

    private sealed class CheckPantry() : Executor<SelectedMeal, PantryCheck>("ReadPantry")
    {
        public override ValueTask<PantryCheck> HandleAsync(
            SelectedMeal meal, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var missing = meal.Recipe?.Ingredients.Where(i =>
                meal.Request.Pantry.TryGetValue(i, out var present) && present == false).ToArray() ?? [];
            return ValueTask.FromResult(new PantryCheck(meal.Request, meal.Recipe, missing));
        }
    }

    private sealed class CheckGoal() : Executor<PantryCheck, ShoppingResult>("CheckGoal")
    {
        public override ValueTask<ShoppingResult> HandleAsync(
            PantryCheck check, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Validate(check.Request, check.Recipe, check.Missing));
        }
    }
}
