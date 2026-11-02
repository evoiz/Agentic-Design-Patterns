This project was built with the microsoft-foundry skill. Before working on or answering questions about foundry agents, read the microsoft-foundry skill first.

One executable is deployed as three independent services. Keep crew prompts and orchestration in Chapter07.MultiAgent. The orchestrator must invoke specialist endpoints rather than fall back to local specialist agents. Use managed identity in Azure and preserve request isolation, cancellation, and explicit remote failure handling.
