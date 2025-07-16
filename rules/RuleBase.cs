using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;

namespace CadQa.Rules
{
    public abstract class RuleBase
    {
        public abstract string Name { get; }

        public abstract IEnumerable<QaIssue> Evaluate(
            Database db,
            Transaction tr);
    }
}
