# Agentic Design Patterns - Code Notebooks

## About
Code notebooks for the book **"Agentic Design Patterns"** by Antonio Gulli.
All 21 chapters have complete, runnable code examples — 66 notebooks covering multiple frameworks per chapter.

## Book Information
- **Title**: Agentic Design Patterns: A Hands-On Guide to Building Intelligent Systems
- **Author**: Antonio Gulli — [LinkedIn](https://www.linkedin.com/in/searchguy/)
- **Pre-order**: [Amazon](https://www.amazon.com/Agentic-Design-Patterns-Hands-Intelligent/dp/3032014018/)
- **Charity**: All author royalties are donated to Save the Children.

## File Structure
Naming convention: `Chapter_XX_Topic_(Variant).ipynb`

Each chapter may have multiple notebooks for different framework implementations (Google ADK, LangChain, CrewAI, etc.).

## Notebooks

### Chapter 1: Prompt Chaining
- `Chapter_01_Prompt_Chaining_(Code_Example).ipynb`
- `Chapter_01_Prompt_Chaining_(JSON_Example).ipynb`

### Chapter 2: Routing
- `Chapter_02_Routing_(Google_ADK).ipynb`
- `Chapter_02_Routing_(LangGraph).ipynb`
- `Chapter_02_Routing_(Openrouter).ipynb`

### Chapter 3: Parallelization
- `Chapter_03_Parallelization_(Google_ADK).ipynb`
- `Chapter_03_Parallelization_(LangChain).ipynb`

### Chapter 4: Reflection
- `Chapter_04_Reflection_(ADK).ipynb`
- `Chapter_04_Reflection_(Iterative_Loop).ipynb`
- `Chapter_04_Reflection_(LangChain).ipynb`

### Chapter 5: Tool Use
- `Chapter_05_Tool_Use_(CrewAI).ipynb`
- `Chapter_05_Tool_Use_(Executing_Code).ipynb`
- `Chapter_05_Tool_Use_(Google_Search).ipynb`
- `Chapter_05_Tool_Use_(LangChain).ipynb`
- `Chapter_05_Tool_Use_(Vertex_AI_Search).ipynb`

### Chapter 6: Planning
- `Chapter_06_Planning_(Code_Example).ipynb`
- `Chapter_06_Planning_(Deep_Research_API).ipynb`

### Chapter 7: Multi-Agent Collaboration
- `Chapter_07_Multi_Agent_(ADK_Gemini_AgentTool).ipynb`
- `Chapter_07_Multi_Agent_(ADK_Gemini_Coordinator).ipynb`
- `Chapter_07_Multi_Agent_(ADK_Gemini_Loop).ipynb`
- `Chapter_07_Multi_Agent_(ADK_Gemini_Parallel).ipynb`
- `Chapter_07_Multi_Agent_(ADK_Gemini_Sequential).ipynb`
- `Chapter_07_Multi_Agent_(CrewAI_Gemini).ipynb`

### Chapter 8: Memory Management
- `Chapter_08_Memory_(ADK_Explicit_State_Update).ipynb`
- `Chapter_08_Memory_(ADK_LlmAgent_output_key).ipynb`
- `Chapter_08_Memory_(ADK_MemoryService_InMemory).ipynb`
- `Chapter_08_Memory_(ADK_SessionService).ipynb`
- `Chapter_08_Memory_(LangChain_LangGraph).ipynb`

### Chapter 9: Learning and Adaptation
- `Chapter_09_Adaptation_(OpenEvolve).ipynb`

### Chapter 10: Model Context Protocol (MCP)
- `Chapter_10_MCP_(ADK_FastMCP_Server).ipynb`
- `Chapter_10_MCP_(FastMCP_Client_Agent_init).ipynb`
- `Chapter_10_MCP_(FastMCP_Server_Example).ipynb`
- `Chapter_10_MCP_(Filesystem_Example_agent).ipynb`
- `Chapter_10_MCP_(Filesystem_Example_init).ipynb`

### Chapter 11: Goal Setting and Monitoring
- `Chapter_11_Goal_Setting_(Iteration).ipynb`

### Chapter 12: Exception Handling and Recovery
- `Chapter_12_Exception_Handling_(Fallback).ipynb`

### Chapter 13: Human-in-the-Loop
- `Chapter_13_Human_in_the_Loop_(Customer_Support).ipynb`

### Chapter 14: Knowledge Retrieval (RAG)
- `Chapter_14_Knowledge_Retrieval_(RAG_Google_Search).ipynb`
- `Chapter_14_Knowledge_Retrieval_(RAG_LangChain).ipynb`
- `Chapter_14_Knowledge_Retrieval_(RAG_VertexAI).ipynb`
- [Public web retrieval with source links (ADK + MCP)](Chapter_14_Knowledge_Retrieval_%28RAG_Parallel_MCP%29.ipynb) — optional anonymous Parallel search with plan, evidence, and cited answer handoffs.

### Chapter 15: Inter-Agent Communication
- `Chapter_15_Inter_Agent_(A2A).ipynb`
- `Chapter_15_Inter_Agent_(A2A_AgentCard_WeatherBot).ipynb`
- `Chapter_15_Inter_Agent_(Sync_Streaming_Requests).ipynb`

### Chapter 16: Resource-Aware Optimization
- `Chapter_16_Resource_Optimization_(Code_Snippets).ipynb`
- `Chapter_16_Resource_Optimization_(OI_Google_Search).ipynb`

### Chapter 17: Reasoning Techniques
- `Chapter_17_Reasoning_(CoT_Prompt).ipynb`
- `Chapter_17_Reasoning_(Executing_Code).ipynb`
- `Chapter_17_Reasoning_(Google_DeepSearch).ipynb`
- `Chapter_17_Reasoning_(Self_Correction).ipynb`

### Chapter 18: Guardrails / Safety Patterns
- `Chapter_18_Guardrails_(ADK_Validate_Tool).ipynb`
- `Chapter_18_Guardrails_(LLM_as_Guardrail).ipynb`
- `Chapter_18_Guardrails_(Practical_Examples).ipynb`

### Chapter 19: Evaluation and Monitoring
- `Chapter_19_Evaluation_(Basic_Response_Evaluation).ipynb`
- `Chapter_19_Evaluation_(LLM_as_Judge).ipynb`

### Chapter 20: Prioritization
- `Chapter_20_Prioritization_(SuperSimplePM).ipynb`

### Chapter 21: Exploration and Discovery
- `Chapter_21_Exploration_Discovery_(Agent_Laboratory).ipynb`

### Appendices
- `Appendix_A_Advanced_Prompting_Techniques.ipynb`
- `Appendix_B_AI_Agentic_From_GUI_to_Real_world_environment.ipynb`
- `Appendix_C_(Code).ipynb`
- `Appendix_C_Quick_overview_of_Agentic_Frameworks.ipynb`
- `Appendix_D_Building_an_Agent_with_AgentSpace.ipynb`
- `Appendix_E_AI_Agents_on_the_CLI.ipynb`
- `Appendix_F_Under_the_Hood_Reasoning_Engines.ipynb`
- `Appendix_G_Coding_agents.ipynb`
- `Appendix_Pydantic.ipynb`

## Getting Started
1. Install Jupyter: `pip install jupyter`
2. Navigate to this folder: `cd chapter_notebooks`
3. Start Jupyter: `jupyter notebook`
4. Open any notebook and start learning!

## Requirements
```bash
pip install jupyter notebook ipykernel
pip install pandas numpy matplotlib  # Common dependencies
```

## Sources
- [Table of Contents](https://docs.google.com/document/d/1rsaK53T3Lg5KoGwvf8ukOUvbELRtH-V0LnOIFDxBryE/edit)
- [Google Drive Folder](https://drive.google.com/drive/u/0/folders/1Y3U3IrYCiJ3E45Z8okR5eCg7OPnWQtPV)
