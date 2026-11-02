using Microsoft.Extensions.AI;

namespace Chapter16.ResourceOptimization.Models;

public sealed record OptimizationModelClient(string Deployment, IChatClient Client);
