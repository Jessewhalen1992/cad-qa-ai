# cad-qa-ai

This repository hosts tooling for automating QA analysis of AutoCAD drawings using rule-based checks and machine learning. It will contain deterministic rule scripts, data exporters, ML experiments, and an AutoCAD plug-in that connects everything together.

⚠️  Build prerequisite  
Define an environment variable called ACADSDK pointing at your AutoCAD 2025 SDK folder (contains acdbmgd.dll).  
Example (PowerShell):  
  setx ACADSDK "C:\Program Files\Autodesk\AutoCAD 2025"  
Restart VS after setting it.

| Phase | Focus |
|-------|------------------------------------------------|
| 1     | Establish basic rule engine and data export    |
| 2     | Integrate ML models for issue detection        |
| 3     | Deploy plug-in with full QA automation         |

