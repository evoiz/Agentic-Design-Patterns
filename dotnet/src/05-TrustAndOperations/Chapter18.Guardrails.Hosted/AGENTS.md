This project adapts the shared Chapter18.Guardrails policy workflow to Foundry Agent Service's Responses protocol.
Keep policy and function-validation logic in the sibling console project. Each hosted request is stateless;
do not persist caller input or bypass the policy middleware.
Read the microsoft-foundry skill before Foundry-specific changes or operations.
