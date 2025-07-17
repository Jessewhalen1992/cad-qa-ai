# cad-qa-ai

This repository hosts tooling for automating QA analysis of AutoCAD drawings using rule-based checks and machine learning. It will contain deterministic rule scripts, data exporters, ML experiments, and an AutoCAD plug-in that connects everything together.

⚠️  Build prerequisite
The project expects the AutoCAD 2025 SDK to be installed in the default
location `C:\Program Files\Autodesk\AutoCAD 2025`. The plug-in project file
(`CadQaPlugin.csproj`) references the SDK assemblies directly from that path.
If your installation lives elsewhere, update the `HintPath` values inside the
project file accordingly.

| Phase | Focus |
|-------|------------------------------------------------|
| 1     | Establish basic rule engine and data export    |
| 2     | Integrate ML models for issue detection        |
| 3     | Deploy plug-in with full QA automation         |

