# 📚 Agentic Design Patterns - A Hands-On Guide to Building Intelligent Systems

[![Book](https://img.shields.io/badge/Book-Pre--order-blue)](https://www.amazon.com/Agentic-Design-Patterns-Hands-Intelligent/dp/3032014018/)
[![Author](https://img.shields.io/badge/Author-Antonio%20Gulli-green)](https://www.linkedin.com/in/searchguy/)
[![Charity](https://img.shields.io/badge/Royalties-Save%20the%20Children-red)](https://www.savethechildren.org/)
[![License](https://img.shields.io/badge/License-Educational-yellow)]()

## 📖 About This Repository

This repository contains the complete materials for **"Agentic Design Patterns: A Hands-On Guide to Building Intelligent Systems"** by Antonio Gulli. It includes all chapters in PDF format and accompanying code notebooks for hands-on learning.

> **Note**: All author royalties are donated to Save the Children 💝

## 🎯 What You'll Learn

This comprehensive guide covers 21 chapters and 7 appendices on building intelligent AI agent systems, including:

- **Foundational Patterns**: Prompt chaining, routing, parallelization
- **Advanced Techniques**: Reflection, tool use, planning, multi-agent systems
- **Memory & Learning**: Memory management, adaptation, goal setting
- **Production Patterns**: Exception handling, human-in-the-loop, RAG
- **Optimization**: Resource-aware patterns, reasoning techniques, guardrails
- **Real-world Applications**: From GUI to real-world environments

## 📁 Repository Structure

```
.
├── 📄 README.md                           # This file
├── 📚 book/
│   └── Agentic_Design_Patterns_Complete.pdf  # Complete book (424 pages)
├── 💻 chapter_notebooks/                          # Chapter 
│   ├── Chapter_01_Prompt_Chaining.ipynb
│   ├── Chapter_02_Routing.ipynb
│   ├── Chapter_03_Parallelization.ipynb
│   ├── ...
│   └── Appendix_G_Coding_Agents.ipynb


```

## 📚 Table of Contents

### Introduction & Foundations
- Dedication
- Acknowledgment
- Foreword
- A Thought Leader's Perspective: Power and Responsibility
- Introduction
- What makes an AI system an "agent"?

### Part One: Core Patterns (103 pages)
1. **Chapter 1**: Prompt Chaining - Sequential task decomposition
2. **Chapter 2**: Routing - Dynamic path selection
3. **Chapter 3**: Parallelization - Concurrent processing
4. **Chapter 4**: Reflection - Self-improvement mechanisms
5. **Chapter 5**: Tool Use - External capability integration
6. **Chapter 6**: Planning - Strategic task management
7. **Chapter 7**: Multi-Agent - Collaborative systems

### Part Two: Advanced Patterns (61 pages)
8. **Chapter 8**: Memory Management - State persistence
9. **Chapter 9**: Learning and Adaptation - Dynamic improvement
10. **Chapter 10**: Model Context Protocol (MCP) - Standardized interfaces
11. **Chapter 11**: Goal Setting and Monitoring - Objective tracking

### Part Three: Production Patterns (34 pages)
12. **Chapter 12**: Exception Handling and Recovery - Robust error management
13. **Chapter 13**: Human-in-the-Loop - Human-AI collaboration
14. **Chapter 14**: Knowledge Retrieval (RAG) - Information access patterns

### Part Four: Enterprise Patterns (114 pages)
15. **Chapter 15**: Inter-Agent Communication (A2A) - Agent networking
16. **Chapter 16**: Resource-Aware Optimization - Efficient resource usage
17. **Chapter 17**: Reasoning Techniques - Advanced decision-making
18. **Chapter 18**: Guardrails/Safety Patterns - Risk mitigation
19. **Chapter 19**: Evaluation and Monitoring - Performance tracking
20. **Chapter 20**: Prioritization - Task management
21. **Chapter 21**: Exploration and Discovery - Autonomous learning

### Appendices (74 pages)
- **Appendix A**: Advanced Prompting Techniques
- **Appendix B**: AI Agentic: From GUI to Real world environment
- **Appendix C**: Quick overview of Agentic Frameworks
- **Appendix D**: Building an Agent with AgentSpace
- **Appendix E**: AI Agents on the CLI
- **Appendix F**: Under the Hood: Reasoning Engines
- **Appendix G**: Coding agents

### Conclusion & References
- Conclusion
- Glossary
- Index of Terms

## 🚀 Getting Started

### Prerequisites

```bash
# Python 3.8 or higher required
python --version

# Install Jupyter for notebooks
pip install jupyter notebook

# Install common dependencies
pip install -r requirements.txt
```

### Installation

1. **Clone the repository**
```bash
git clone https://github.com/evoiz/Agentic-Design-Patterns.git
cd Agentic-Design-Patterns.git
```

2. **Set up virtual environment** (recommended)
```bash
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate
```

3. **Install dependencies**
```bash
pip install jupyter notebook
pip install pandas numpy matplotlib openai langchain
```

4. **Launch Jupyter Notebook**
```bash
jupyter notebook
```

## 💻 Running the Code

Each chapter includes a Jupyter notebook with practical examples:

1. Navigate to the `chapter_notebooks/` directory
2. Open the desired chapter notebook
3. Follow the instructions within each notebook
4. Run cells sequentially for best learning experience

### Example: Running Chapter 1
```python
# Navigate to notebooks directory
cd chapter_notebooks

# Launch specific notebook
jupyter notebook Chapter_01_Prompt_Chaining.ipynb
```

## 📖 How to Use This Repository

### For Self-Study
1. Read each chapter in the PDF
2. Open the corresponding notebook
3. Run the code examples
4. Experiment with modifications
5. Complete the exercises

### For Teaching
1. Use chapters as lecture materials
2. Assign notebooks as lab exercises
3. Create custom examples based on patterns
4. Build projects using multiple patterns

### For Research
1. Reference implementation patterns
2. Benchmark different approaches
3. Extend patterns for new use cases
4. Contribute improvements back

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md).

### Ways to Contribute
- 🐛 Report bugs and issues
- 💡 Suggest new features or patterns
- 📝 Improve documentation
- 🔧 Submit code improvements
- 🌍 Translate materials

### Contribution Process
1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📚 Additional Resources

### Official Links
- 📖 [Pre-order the Book](https://www.amazon.com/Agentic-Design-Patterns-Hands-Intelligent/dp/3032014018/)
- 👨‍💼 [Author's LinkedIn](https://www.linkedin.com/in/searchguy/)
- 📁 [Original Google Drive Materials](https://drive.google.com/drive/u/0/folders/1Y3U3IrYCiJ3E45Z8okR5eCg7OPnWQtPV)

### Related Frameworks
- [LangChain](https://langchain.com/)
- [AutoGPT](https://github.com/Significant-Gravitas/AutoGPT)
- [OpenAI Assistants](https://platform.openai.com/assistants)
- [Microsoft AutoGen](https://github.com/microsoft/autogen)
- [CrewAI](https://www.crewai.com/)

### Learning Path
1. **Beginners**: Start with Chapters 1-7 (Core Patterns)
2. **Intermediate**: Progress through Chapters 8-14 (Advanced & Production)
3. **Advanced**: Master Chapters 15-21 (Enterprise Patterns)
4. **Experts**: Explore Appendices for cutting-edge techniques

## ⚖️ License

This repository is for educational purposes. Please respect the author's copyright and intellectual property rights.

- **Book Content**: © Antonio Gulli - All rights reserved
- **Code Examples**: MIT License (see LICENSE file)
- **Educational Use**: Permitted with attribution

## 🙏 Acknowledgments

- **Antonio Gulli** - Author and AI thought leader
- **Save the Children** - Beneficiary of all book royalties
- **Contributors** - Everyone who helps improve these materials
- **Community** - Learners and practitioners advancing AI agents

## 📞 Contact & Support

- **Issues**: [GitHub Issues](https://github.com/evoiz/Agentic-Design-Patterns/issues)
- **Discussions**: [GitHub Discussions](https://github.com/evoiz/Agentic-Design-Patterns/discussions)
- **Author**: [LinkedIn](https://www.linkedin.com/in/searchguy/)

## 🌟 Star History

If you find this repository useful, please consider giving it a star ⭐

[![Star History Chart](https://api.star-history.com/svg?repos=evoiz/agentic-design-patterns&type=Date)](https://star-history.com/#evoiz/agentic-design-patterns&Date)

## 📊 Repository Stats

![GitHub last commit](https://img.shields.io/github/last-commit/evoiz/agentic-design-patterns)
![GitHub issues](https://img.shields.io/github/issues/evoiz/agentic-design-patterns)
![GitHub pull requests](https://img.shields.io/github/issues-pr/evoiz/agentic-design-patterns)
![GitHub](https://img.shields.io/github/license/evoiz/agentic-design-patterns)

---

<p align="center">
  <strong>Building the future of AI, one pattern at a time 🚀</strong>
</p>

<p align="center">
  Made with ❤️ for the AI community
</p>