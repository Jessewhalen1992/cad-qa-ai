using Autodesk.AutoCAD.Runtime;       // [CommandMethod]
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using CadQa.Export;
using CadQa.Rules;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CadQaPlugin
{
    public class QaChecker
    {
        [CommandMethod("RUNQAAUDIT")]
        public static void RunQaAudit()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using var tr = db.TransactionManager.StartTransaction();

            // 1️⃣  deterministic rules
            var issues = new RuleBase[] { new TextStyleRule() }
                .SelectMany(r => r.Evaluate(db, tr))
                .ToList();

            // 2️⃣  dump features
            ExportFeatures.DumpText(
                db, tr,
                Path.ChangeExtension(db.Filename, ".features.csv"));

            // 3️⃣  collect annotation strings
            var texts = new List<string>();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);

                // ── NEW: ignore any entity on a “Z‑” layer ────────────────
                if (ent is Entity e &&
                    e.Layer.StartsWith("Z-", StringComparison.OrdinalIgnoreCase))
                    continue;
                // ──────────────────────────────────────────────────────────

                if (ent is DBText t && !string.IsNullOrWhiteSpace(t.TextString))
                    texts.Add(t.TextString);

                else if (ent is MText m && !string.IsNullOrWhiteSpace(m.Contents))
                    texts.Add(m.Contents);
            }

            // 4️⃣  machine‑learning suggestions
            if (texts.Count > 0)
            {
                try
                {
                    PythonEngine.Initialize();
                    using (Py.GIL())
                    {
                        dynamic joblib = Py.Import("joblib");
                        dynamic model = joblib.load("ml/artifacts/layer_clf.pkl");
                        dynamic preds = model.predict(texts.ToArray());

                        for (int i = 0; i < texts.Count; i++)
                        {
                            doc.Editor.WriteMessage(
                                $"\nML suggestion: '{texts[i]}' → '{preds[i]}'");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\nML error: {ex.Message}");
                }
                finally
                {
                    if (PythonEngine.IsInitialized)
                        PythonEngine.Shutdown();
                }
            }

            // 5️⃣  write deterministic issues
            var simple = issues.Select(i => new
            {
                i.Id,
                Type = i.Type.ToString(),
                Handle = i.EntityId.ToString(),
                i.Message
            });
            File.WriteAllText(
                Path.ChangeExtension(db.Filename, ".qa.json"),
                JsonSerializer.Serialize(simple, new JsonSerializerOptions { WriteIndented = true }));

            doc.Editor.WriteMessage($"\nQA issues found: {issues.Count}");
        }
    }
}
