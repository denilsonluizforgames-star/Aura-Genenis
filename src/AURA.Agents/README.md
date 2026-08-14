# AURA.Agents

Placeholder for the agent system (HealthAgent, WindowsAgent, MemoryAgent,
AIAgent, AutomationAgent) described in the Genesis 2.0 architecture. Not
implemented in the Genesis Core MVP (1.0) - scheduled for AURA 1.1, per the
roadmap in the master prompt ("Próximas versões").

Only the `IAgent` contract (in `AURA.Core.Abstractions`) exists today so the
rest of the codebase can reference it without a circular dependency once
concrete agents land here.
