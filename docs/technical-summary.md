# Technical project summary

AutoInsight AI Agent is a local-first CSV analytics application built with ASP.NET Core, C#, React, TypeScript, CsvHelper, Recharts, and Microsoft Semantic Kernel. Its deterministic services parse bounded uploads, infer schema, calculate descriptive statistics, identify missing values and duplicates, detect IQR outliers, generate chart specifications, and create Markdown reports. A credential-free Demo Mode and an optional model-backed Agent Mode share the same tested C# analysis functions while remaining visibly distinct.

The security model treats CSV content as untrusted data, restricts the model to allowlisted functions, minimizes external tool payloads, stores credentials server-side, enforces request and tool-call limits, retains at most ten in-memory datasets, and accepts loopback clients only by default. Automated xUnit, API integration, Vitest, lint, build, dependency-audit, and GitHub Actions checks cover the reproducible path. The Gemini connector is prerelease, and provider-backed behavior is not claimed as verified until the five-scenario evaluation succeeds with a locally configured credential.

## Concise repository description

Local-first CSV analytics with ASP.NET Core, React, deterministic statistics, interactive charts, and restricted Semantic Kernel agent tools.

## Academic CV entry

**AutoInsight AI Agent — Independent full-stack and agentic AI project.** Built a local-first CSV analytics platform using ASP.NET Core, C#, React, TypeScript, and Microsoft Semantic Kernel. Implemented bounded parsing, deterministic profiling and statistics, data-quality diagnostics, interactive charts, restricted agent function calling, privacy controls, and automated backend/API/frontend validation with GitHub Actions.
