using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadQa.Rules
{
    public abstract class RuleBase
    {
        public abstract string Name { get; }

        public abstract IEnumerable<QaIssue> Evaluate(Database db, Transaction tr);
    }
}
