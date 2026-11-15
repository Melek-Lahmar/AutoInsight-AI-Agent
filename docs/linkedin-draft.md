# LinkedIn publication draft

I’ve been exploring AI agents and agentic AI through the 365 Data Science learning experience, and I wanted to turn the concepts into a practical full-stack project.

I built **AutoInsight AI Agent**, a local-first CSV analysis platform using C#, ASP.NET Core, React, TypeScript, Recharts, CsvHelper, and Microsoft Semantic Kernel.

The application can:

- profile an uploaded dataset and infer column types;
- calculate descriptive statistics and category frequencies;
- detect missing values, duplicate rows, mixed types, constant columns, category imbalance, and potential IQR outliers;
- generate interactive chart specifications;
- show exactly which analysis tools an assistant executes;
- export a transparent Markdown report with limitations.

One of the most important design decisions was to separate two modes. Demo Mode works without credentials and uses deterministic C# orchestration. AI Agent Mode uses real Semantic Kernel function calling with Gemini or OpenAI, but only through an allowlisted set of analysis tools. Raw dataset rows stay local by default, model inputs are minimized, and generated explanations must be grounded in calculated tool output.

The most useful lesson was that “agentic” behavior is not the chat interface. The engineering value comes from bounded tools, observable execution, factual grounding, failure handling, privacy controls, and tests.

This project strengthened my understanding of full-stack integration, statistical profiling, API design, agent orchestration, and the difference between deterministic software behavior and LLM interpretation.

GitHub: https://github.com/Melek-Lahmar/AutoInsight-AI-Agent

`#ArtificialIntelligence #AIAgents #AgenticAI #CSharp #DotNET #React #TypeScript #DataScience #SoftwareEngineering`

> Publishing note: add the exact certificate wording only after you have confirmed that the credential has been awarded. The wording above intentionally describes a learning experience rather than claiming certification.
