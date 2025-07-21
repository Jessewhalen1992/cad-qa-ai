using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.Collections.Generic;

namespace CadQa.Rules
{
    /// <summary>Checks that common block symbols live on the correct layer.</summary>
    public class BlockLayerRule : RuleBase
    {
        private readonly Dictionary<string,string> _map = new()
        {
            // BlockName (substring/uppercase) -> ExpectedLayer
            { "HYDRANT",  "L-WATR" },
            { "VALVE",    "L-MECH" },
            { "CATCHBAS", "L-DRAIN" }
            // ⬆️ extend later as needed
        };

        public override string Name => "Block Layer Rule";

        public override IEnumerable<QaIssue> Evaluate(Database db, Transaction tr)
        {
            var bt  = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms  = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not BlockReference br) continue;
                var blkName = br.Name.ToUpperInvariant();

                foreach (var kvp in _map)
                {
                    if (blkName.Contains(kvp.Key) &&
                        !br.Layer.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return new QaIssue
                        {
                            Id       = id.Handle.Value.ToInt(),
                            Type     = IssueType.Layer,
                            EntityId = id,
                            Message  = $"Block '{br.Name}' should be on layer '{kvp.Value}' (found '{br.Layer}')."
                        };
                    }
                }
            }
        }
    }
}
