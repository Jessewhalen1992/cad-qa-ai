using System.Linq;
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

            // TODO: write JSON next to drawing
        }
    }
}
