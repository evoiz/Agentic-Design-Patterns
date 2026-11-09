using Microsoft.Extensions.AI;

namespace ResourceOptimization.Models;

public sealed record OptimizationModelClient(string Deployment, IChatClient Client);
