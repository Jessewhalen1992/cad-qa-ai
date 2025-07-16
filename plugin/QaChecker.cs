using System.IO;
using System.Linq;
using System.Text.Json;
using CadQa.Export;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
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

            var textCsv = $"{db.Filename}.text.csv";
            ExportFeatures.DumpText(db, tr, textCsv);

            var jsonPath = $"{db.Filename}.qa.json";
            var json = JsonSerializer.Serialize(
                issues,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(jsonPath, json);

            Application.DocumentManager
                .MdiActiveDocument
                .Editor
                .WriteMessage($"\nQA issues: {issues.Count}");
        }
    }
}
