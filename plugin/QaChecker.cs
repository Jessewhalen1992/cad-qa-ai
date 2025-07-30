using Autodesk.AutoCAD.Runtime;   // [CommandMethod]
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
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
        // ------------------------------------------------------------------
        [CommandMethod("RUNQAAUDIT")]
        public static void RunQaAudit() => RunQaAuditImpl(null);

        [CommandMethod("RUNQAAUDITSEL")]
        public static void RunQaAuditSel()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var sel = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = "\nSelect linework to audit (press Enter to select all):"
            });
            if (sel.Status == PromptStatus.Cancel) return;

            RunQaAuditImpl(sel.Status == PromptStatus.OK
                ? new HashSet<ObjectId>(sel.Value.GetObjectIds())
                : null);
        }

        // ------------------------------------------------------------------
        private static void RunQaAuditImpl(HashSet<ObjectId> selectedIds)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();

            // 1 deterministic rules
            var issues = new RuleBase[] { new TextStyleRule(), new BlockLayerRule() }
                .SelectMany(r => r.Evaluate(db, tr))
                .Where(i => selectedIds == null || selectedIds.Contains(i.EntityId))
                .ToList();

            // 2 feature export (optional)
            ExportFeatures.DumpFeatures(db, tr,
                Path.ChangeExtension(db.Filename, ".features.csv"));

            // 3 collect text for ML
            var texts = new List<string>();
            var layers = new List<string>();
            var xs = new List<double?>();
            var ys = new List<double?>();

            var ms = (BlockTableRecord)tr.GetObject(
                        ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))
                        [BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                if (selectedIds != null && !selectedIds.Contains(id)) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                if (ent.Layer.StartsWith("Z-", StringComparison.OrdinalIgnoreCase)) continue;

                switch (ent)
                {
                    case DBText t when !string.IsNullOrWhiteSpace(t.TextString)
                                      && !IsMostlyNumeric(t.TextString):
                        texts.Add(t.TextString); layers.Add(t.Layer);
                        xs.Add(t.Position.X); ys.Add(t.Position.Y);
                        break;

                    case MText m when !string.IsNullOrWhiteSpace(m.Text)
                                      && !IsMostlyNumeric(m.Text):
                        texts.Add(m.Text); layers.Add(m.Layer);
                        xs.Add(m.Location.X); ys.Add(m.Location.Y);
                        break;
                }
            }

            // 4 ML suggestions → qa_suggest.tsv
            string suggestPath = null;
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

                        const char tabChar = '\t';
                        var rows = new List<string> { "Text\tX\tY\tCurrentLayer\tSuggestedLayer" };

                        for (int i = 0; i < texts.Count; i++)
                        {
                            string predicted = ((string)preds[i]).Trim();
                            string current = (layers[i] ?? "").Trim();
                            if (predicted.Equals(current, StringComparison.OrdinalIgnoreCase))
                                continue;   // skip OK items

                            string txt = texts[i].Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
                            string sx = xs[i]?.ToString("F3") ?? "";
                            string sy = ys[i]?.ToString("F3") ?? "";
                            rows.Add($"{txt}{tabChar}{sx}{tabChar}{sy}{tabChar}{current}{tabChar}{predicted}");
                        }

                        if (rows.Count > 1)
                        {
                            suggestPath = Path.ChangeExtension(db.Filename, ".qa_suggest.tsv");
                            File.WriteAllLines(suggestPath, rows);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage($"\nML error: {ex.Message}");
                }
                finally
                {
                    if (PythonEngine.IsInitialized) PythonEngine.Shutdown();
                }
            }

            // 5 JSON issue dump
            File.WriteAllText(
                Path.ChangeExtension(db.Filename, ".qa.json"),
                JsonSerializer.Serialize(
                    issues.Select(i => new
                    {
                        i.Id,
                        Type = i.Type.ToString(),
                        Handle = i.EntityId.ToString(),
                        i.Message
                    }),
                    new JsonSerializerOptions { WriteIndented = true }));

            // 6 summary CSV
            File.WriteAllLines(
                Path.ChangeExtension(db.Filename, ".qa_summary.csv"),
                new[] { "IssueType,Count" }.Concat(
                    issues.GroupBy(i => i.Type.ToString())
                          .Select(g => $"{g.Key},{g.Count()}")));

            // 7 detail TSV
            var detailPath = Path.ChangeExtension(db.Filename, ".qa_detail.tsv");
            const char tab = '\t';
            var dLines = new List<string> { "IssueType\tHandle\tX\tY\tContent\tMessage" };

            foreach (var isue in issues)
            {
                double? x = null, y = null; string content = "";
                try
                {
                    if (tr.GetObject(isue.EntityId, OpenMode.ForRead) is Entity e)
                    {
                        switch (e)
                        {
                            case DBText txt: x = txt.Position.X; y = txt.Position.Y; content = txt.TextString; break;
                            case MText mt: x = mt.Location.X; y = mt.Location.Y; content = mt.Text; break;
                            case BlockReference br:
                                x = br.Position.X; y = br.Position.Y; content = br.Name; break;
                        }
                    }
                }
                catch { /* ignore */ }

                string nx = x?.ToString("F3") ?? "";
                string ny = y?.ToString("F3") ?? "";
                string safeContent = (content ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
                string safeMsg = (isue.Message ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
                dLines.Add($"{isue.Type}{tab}{isue.EntityId}{tab}{nx}{tab}{ny}{tab}{safeContent}{tab}{safeMsg}");
            }
            File.WriteAllLines(detailPath, dLines);

            // 8 console paths
            doc.Editor.WriteMessage($"\nQA issues found       : {issues.Count}");
            doc.Editor.WriteMessage($"\nDetail TSV   : {detailPath}");
            doc.Editor.WriteMessage($"\nSummary CSV  : {Path.ChangeExtension(db.Filename, ".qa_summary.csv")}");
            if (suggestPath != null)
                doc.Editor.WriteMessage($"\nLayer suggestions TSV : {suggestPath}");
        }

        private static bool IsMostlyNumeric(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            int digit = 0, alpha = 0;
            foreach (char c in s)
            {
                if (char.IsDigit(c)) digit++;
                else if (char.IsLetter(c)) alpha++;
            }
            return digit > 0 && alpha == 0;
        }
    }
}
