using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.IO;

namespace CadQa.Commands
{
    public class BatchExport
    {
        // Exposed as QA_EXPORT_BATCH
        [CommandMethod("QA_EXPORT_BATCH", CommandFlags.Session)]
        public void ExportFolder()
        {
            var ed = Autodesk.AutoCAD.ApplicationServices
                       .Core.Application.DocumentManager
                       .MdiActiveDocument.Editor;

            // Prompt for folder (default = current) – allow spaces
            var pr = new PromptStringOptions(
                "\nFolder containing DWGs to export <current>:")
            {
                DefaultValue = Environment.CurrentDirectory,
                AllowSpaces = true                           // ← key fix
            };
            var res = ed.GetString(pr);
            if (res.Status != PromptStatus.OK) return;

            var folder = Path.GetFullPath(res.StringResult);
            if (!Directory.Exists(folder))
            {
                ed.WriteMessage($"\nFolder not found: {folder}");
                return;
            }

            int ok = 0, fail = 0;

            foreach (var dwg in Directory.EnumerateFiles(folder, "*.dwg"))
            {
                try
                {
                    using var db = new Database(false, true);
                    db.ReadDwgFile(dwg, FileShare.ReadWrite, true, "");
                    using var tr = db.TransactionManager.StartTransaction();

                    var csv = Path.ChangeExtension(dwg, ".Text.csv");
                    CadQa.Export.ExportFeatures.DumpFeatures(db, tr, csv);
                    tr.Commit();
                    ok++;
                }
                catch (System.Exception ex)                  // ← disambiguated
                {
                    ed.WriteMessage($"\nError on {Path.GetFileName(dwg)}: {ex.Message}");
                    fail++;
                }
            }

            ed.WriteMessage($"\nBatch complete: {ok} OK, {fail} failed.");
        }
    }
}
