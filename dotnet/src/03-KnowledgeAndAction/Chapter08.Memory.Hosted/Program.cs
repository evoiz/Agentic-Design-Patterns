using Chapter08.Memory.Hosted;
using Microsoft.Agents.AI.Foundry.Hosting;

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(new MemoryDemoAgent());
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
var app = builder.Build();
app.Run();
