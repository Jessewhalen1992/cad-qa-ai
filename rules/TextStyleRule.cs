using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadQa.Rules
{
    public class TextStyleRule : RuleBase
    {
        public override string Name => "TextStyleRule";

        public override IEnumerable<QaIssue> Evaluate(Database db, Transaction tr)
        {
            // Look up the "ROMANS" text‑style once
            var styleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            ObjectId romansId = styleTable.Has("ROMANS") ? styleTable["ROMANS"] : ObjectId.Null;

            // Model‑space record
            var ms = (BlockTableRecord)tr.GetObject(
                         ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))
                         [BlockTableRecord.ModelSpace],
                         OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                // ⬇ NEW  — Skip anything on a layer that begins with “Z‑”
                if (ent.Layer.StartsWith("Z-", StringComparison.OrdinalIgnoreCase))
                    continue;

                // -----------------------------------------------------------------
                // DBText  ----------------------------------------------------------
                if (ent is DBText dbText)
                {
                    var tsr = (TextStyleTableRecord)tr.GetObject(
                                  dbText.TextStyleId, OpenMode.ForRead);

                    if (!tsr.Name.Equals("ROMANS", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return new QaIssue
                        {
                            Type = IssueType.TextStyle,
                            EntityId = id,
                            Message = $"Text style should be ROMANS; found {tsr.Name}",
                            FixAction = () =>
                            {
                                var txt = (DBText)tr.GetObject(id, OpenMode.ForWrite);
                                txt.TextStyleId = romansId;
                            }
                        };
                    }
                }
                // -----------------------------------------------------------------
                // MText  -----------------------------------------------------------
                else if (ent is MText mtext)
                {
                    var tsr = (TextStyleTableRecord)tr.GetObject(
                                  mtext.TextStyleId, OpenMode.ForRead);

                    if (!tsr.Name.Equals("ROMANS", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return new QaIssue
                        {
                            Type = IssueType.TextStyle,
                            EntityId = id,
                            Message = $"Text style should be ROMANS; found {tsr.Name}",
                            FixAction = () =>
                            {
                                var txt = (MText)tr.GetObject(id, OpenMode.ForWrite);
                                txt.TextStyleId = romansId;
                            }
                        };
                    }
                }
            }
        }
    }
}
