using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.IO;

namespace CadQa.Export
{
    public static class ExportFeatures
    {
        public static void DumpText(Database db, Transaction tr, string csvPath)
        {
            using var sw = new StreamWriter(csvPath);
            sw.WriteLine("Handle,ObjType,TextString,Layer,TextHeight");

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);

                // ── NEW: ignore any entity on a “Z‑” layer ────────────────
                if (ent is Entity e &&
                    e.Layer.StartsWith("Z-", StringComparison.OrdinalIgnoreCase))
                    continue;
                // ──────────────────────────────────────────────────────────

                switch (ent)
                {
                    case DBText t:
                        sw.WriteLine(
                            $"{id.Handle},{nameof(DBText)},\"{t.TextString}\",{t.Layer},{t.Height}");
                        break;

                    case MText m:
                        sw.WriteLine(
                            $"{id.Handle},{nameof(MText)},\"{m.Contents}\",{m.Layer},{m.TextHeight}");
                        break;
                }
            }
        }
    }
}
