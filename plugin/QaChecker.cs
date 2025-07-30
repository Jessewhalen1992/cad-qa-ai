// QaChecker.cs  –  confidence‑threshold build (TextStyleRule removed)
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
        public static void RunQaAudit() => RunImpl(null);

        [CommandMethod("RUNQAAUDITSEL")]
        public static void RunQaAuditSel()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var sel = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding =
                    "\nSelect linework to audit (press Enter to select all):"
            });
            if (sel.Status == PromptStatus.Cancel) return;

            RunImpl(sel.Status == PromptStatus.OK
                ? new HashSet<ObjectId>(sel.Value.GetObjectIds())
                : null);
        }

        // ------------------------------------------------------------------
        private static void RunImpl(HashSet<ObjectId>? selectedIds)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();

            // 1 ─ deterministic rules (TextStyleRule removed)
            var issues = new RuleBase[] { new BlockLayerRule() }
                .SelectMany(r => r.Evaluate(db, tr))
                .Where(i => selectedIds == null || selectedIds.Contains(i.EntityId))
                .ToList();

            // 2 ─ optional feature export
            ExportFeatures.DumpFeatures(
                db, tr, Path.ChangeExtension(db.Filename, ".features.csv"));

            // 3 ─ gather annotation strings
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

            // 4 ─ ML suggestions with confidence filter
            const double CONF_THRESHOLD = 0.60;          // new threshold
            string? suggestPath = null;

            if (texts.Count > 0)
            {
                try
                {
                    PythonEngine.Initialize();
                    using (Py.GIL())
                    {
                        dynamic joblib = Py.Import("joblib");
                        dynamic np = Py.Import("numpy");

                        dynamic model = joblib.load("ml/artifacts/layer_clf.pkl");
                        dynamic preds = model.predict(texts.ToArray());
                        dynamic probs = model.predict_proba(texts.ToArray());

                        var rows = new List<string>
                        { "Text\tX\tY\tCurrentLayer\tSuggestedLayer\tConfidence" };
                        const char tab = '\t';

                        for (int i = 0; i < texts.Count; i++)
                        {
                            // highest probability for this sample
                            double best = ((double[])probs[i]
                                          .AsManagedObject(typeof(double[]))).Max();

                            string suggestion = best >= CONF_THRESHOLD
                                ? ((string)preds[i]).Trim()
                                : "No Suggested Layer";

                            string current = layers[i] ?? "";
                            if (suggestion.Equals(current, StringComparison.OrdinalIgnoreCase))
                                continue;                            // skip if already correct

                            string cleanText = texts[i]
                                               .Replace("\t", " ")
                                               .Replace("\r", " ")
                                               .Replace("\n", " ");
                            string sx = xs[i]?.ToString("F3") ?? "";
                            string sy = ys[i]?.ToString("F3") ?? "";
                            rows.Add($"{cleanText}{tab}{sx}{tab}{sy}{tab}" +
                                     $"{current}{tab}{suggestion}{tab}{best:0.00}");
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

            // 5 ─ deterministic issues → .qa.json
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

            // 6 ─ summary CSV
            File.WriteAllLines(
                Path.ChangeExtension(db.Filename, ".qa_summary.csv"),
                new[] { "IssueType,Count" }.Concat(
                    issues.GroupBy(i => i.Type.ToString())
                          .Select(g => $"{g.Key},{g.Count()}")));

            // 7 ─ detail TSV
            var detailPath = Path.ChangeExtension(db.Filename, ".qa_detail.tsv");
            const char tab2 = '\t';
            var dLines = new List<string>
            { "IssueType\tHandle\tX\tY\tContent\tMessage" };

            foreach (var isue in issues)
            {
                double? x = null, y = null; string content = "";
                try
                {
                    if (tr.GetObject(isue.EntityId, OpenMode.ForRead) is Entity e)
                    {
                        switch (e)
                        {
                            case DBText t: x = t.Position.X; y = t.Position.Y; content = t.TextString; break;
                            case MText m: x = m.Location.X; y = m.Location.Y; content = m.Text; break;
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

                dLines.Add($"{isue.Type}{tab2}{isue.EntityId}{tab2}{nx}{tab2}{ny}{tab2}{safeContent}{tab2}{safeMsg}");
            }
            File.WriteAllLines(detailPath, dLines);

            // 8 ─ console summary
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
