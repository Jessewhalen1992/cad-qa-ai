using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using CadQa.Export;
using CadQa.Rules;

namespace CadQaPlugin
{
    public class QaChecker
    {
        [CommandMethod("RUNQAAUDIT")]
        public static void RunQaAudit()
        {
            var db = Application.DocumentManager
                .MdiActiveDocument
                .Database;

            using var tr = db.TransactionManager.StartTransaction();

            var rules = new RuleBase[]
            {
                new TextStyleRule()
            };

            var issues = rules
                .SelectMany(r => r.Evaluate(db, tr))
                .ToList();

            // Dump text features to CSV
            ExportFeatures.DumpText(
                db,
                tr,
                Path.ChangeExtension(db.Filename, ".features.csv"));

            // Write QA issues to indented JSON
            var jsonPath = Path.ChangeExtension(db.Filename, ".qa.json");
            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(issues, opts));

            // Echo summary to command line
            Application.DocumentManager.MdiActiveDocument.Editor
                .WriteMessage($"\nQA issues found: {issues.Count}");
        }
    }
}
