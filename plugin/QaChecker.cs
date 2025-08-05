// QaChecker.cs – minimal output build + JSON configuration support
using Autodesk.AutoCAD.Runtime;   // [CommandMethod]
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
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
        // ---------- CONFIG -------------------------------------------------
        private class QaConfig
        {
            public double ConfidenceThreshold { get; set; } = 0.60;
            public string[] IgnoreLayers { get; set; } =
                { "DEFPOINTS", "VIEWPORTS", "0" };
            public string OutputFolder { get; set; } = "%DWGDIR%";
            public bool VerboseReports { get; set; } = false;
        }
        private static readonly QaConfig Cfg = LoadConfig();

        private static QaConfig LoadConfig()
        {
            // Look for qa_config.json next to DLL, then next to drawing
            string dllDir = Path.GetDirectoryName(
                typeof(QaChecker).Assembly.Location)!;
            string dwgDir = Path.GetDirectoryName(
                Application.DocumentManager.MdiActiveDocument.Database.Filename)!;

            foreach (string path in new[]
            {
                Path.Combine(dllDir, "qa_config.json"),
                Path.Combine(dwgDir, "qa_config.json")
            })
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<QaConfig>(
                            File.ReadAllText(path)) ?? new QaConfig();
                    }
                    catch { /* bad JSON → ignore */ }
                }
            }
            return new QaConfig();  // defaults
        }
        // ------------------------------------------------------------------

        [CommandMethod("RUNQAAUDIT")] public static void RunQaAudit() => RunImpl(null, false);
        [CommandMethod("RUNQAAUDITSEL")] public static void RunQaAuditSel() => SelectAndRun(false);
        [CommandMethod("RUNQAAUDITFIX")] public static void RunQaAuditFix() => RunImpl(null, true);
        [CommandMethod("RUNQAAUDITSELFIX")] public static void RunQaAuditSelFix() => SelectAndRun(true);

        private static void SelectAndRun(bool applyFixes)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var sel = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = "\nSelect linework to audit (press Enter to select all):"
            });
            if (sel.Status == PromptStatus.Cancel) return;

            RunImpl(sel.Status == PromptStatus.OK
                        ? new HashSet<ObjectId>(sel.Value.GetObjectIds())
                        : null,
                    applyFixes);
        }

        // ------------------------------------------------------------------
        private static void RunImpl(HashSet<ObjectId>? selectedIds, bool applyFixes)
        {
            // helper – yyyymmdd_HHMMSS before extension, honour OutputFolder
            static string Stamp(string basePath)
            {
                string dir = Cfg.OutputFolder.Trim() switch
                {
                    "%DWGDIR%" => Path.GetDirectoryName(basePath)!,
                    "%DLLDIR%" => Path.GetDirectoryName(
                                    typeof(QaChecker).Assembly.Location)!,
                    "" => Path.GetDirectoryName(basePath)!,
                    var custom => custom
                };
                Directory.CreateDirectory(dir);
                return Path.Combine(dir,
                    $"{Path.GetFileNameWithoutExtension(basePath)}_" +
                    $"{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(basePath)}");
            }

            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            using var tr = db.TransactionManager.StartTransaction();

            // deterministic rules (kept, but only summarised in console)
            var issues = new RuleBase[] { new BlockLaye, new SpellCheckRule()rRule() }
                .SelectMany(r => r.Evaluate(db, tr))
                .Where(i => selectedIds == null || selectedIds.Contains(i.EntityId))
                .ToList();

            // collect text for ML
            var ids = new List<ObjectId>(); var texts = new List<string>();
            var layers = new List<string>(); var xs = new List<double?>(); var ys = new List<double?>();

            var ms = (BlockTableRecord)tr.GetObject(
                        ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))
                        [BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                if (selectedIds != null && !selectedIds.Contains(id)) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;

                // ---------- IGNORE LIST + Z- rule ---------------------------
                if (ent.Layer.StartsWith("Z-", StringComparison.OrdinalIgnoreCase) ||
                    Cfg.IgnoreLayers.Contains(ent.Layer, StringComparer.OrdinalIgnoreCase))
                    continue;
                // ------------------------------------------------------------

                switch (ent)
                {
                    case DBText t when !string.IsNullOrWhiteSpace(t.TextString)
                                      && !IsMostlyNumeric(t.TextString):
                        ids.Add(id); texts.Add(t.TextString); layers.Add(t.Layer);
                        xs.Add(t.Position.X); ys.Add(t.Position.Y);
                        break;
                    case MText m when !string.IsNullOrWhiteSpace(m.Text)
                                      && !IsMostlyNumeric(m.Text):
                        ids.Add(id); texts.Add(m.Text); layers.Add(m.Layer);
                        xs.Add(m.Location.X); ys.Add(m.Location.Y);
                        break;
                }
            }

            // ML suggestions
            double CONF = Cfg.ConfidenceThreshold;
            int fixedCnt = 0; string? csvPath = null;

            if (texts.Count > 0)
            {
                if (!PythonEngine.IsInitialized) PythonEngine.Initialize();
                using (Py.GIL())
                {
                    dynamic joblib = Py.Import("joblib");
                    dynamic model = joblib.load("ml/artifacts/layer_clf.pkl");
                    dynamic preds = model.predict(texts.ToArray());
                    dynamic probs = model.predict_proba(texts.ToArray());

                    var rows = new List<string>
                    { "Text,X,Y,CurrentLayer,SuggestedLayer,Confidence,Status" };

                    for (int i = 0; i < texts.Count; i++)
                    {
                        double best = ((double[])probs[i]
                                       .AsManagedObject(typeof(double[]))).Max();
                        string sugg = best >= CONF ? ((string)preds[i]).Trim() : "No Suggested Layer";
                        string curr = layers[i] ?? "";
                        bool same = sugg.Equals(curr, StringComparison.OrdinalIgnoreCase);

                        bool willFix = applyFixes && !same &&
                                       !sugg.Equals("No Suggested Layer", StringComparison.OrdinalIgnoreCase);

                        string status = willFix ? "Fixed" : (same ? "OK" : "Suggested");

                        if (willFix)
                        {
                            if (tr.GetObject(ids[i], OpenMode.ForWrite) is Entity e)
                                e.Layer = sugg;
                            fixedCnt++;
                        }

                        if (status == "OK") continue;  // hide unchanged

                        string clean = texts[i].Replace(',', ' ')
                                               .Replace('\r', ' ')
                                               .Replace('\n', ' ');
                        string sx = xs[i]?.ToString("F3") ?? "";
                        string sy = ys[i]?.ToString("F3") ?? "";
                        rows.Add($"{clean},{sx},{sy},{curr},{sugg},{best:0.00},{status}");
                    }

                    if (rows.Count > 1)
                    {
                        csvPath = Stamp(Path.ChangeExtension(db.Filename, ".qa_layers.csv"));
                        File.WriteAllLines(csvPath, rows);
                    }
                }
            }

            // console summary
            doc.Editor.WriteMessage($"\nDeterministic issues  : {issues.Count}");
            doc.Editor.WriteMessage($"\nAuto-fixed layers     : {fixedCnt}");
            if (csvPath != null)
                doc.Editor.WriteMessage($"\nLayer review CSV      : {csvPath}");

            tr.Commit();
        }

        private static bool IsMostlyNumeric(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            int d = 0, a = 0; foreach (char c in s)
            { if (char.IsDigit(c)) d++; else if (char.IsLetter(c)) a++; }
            return d > 0 && a == 0;
        }
    }
}
