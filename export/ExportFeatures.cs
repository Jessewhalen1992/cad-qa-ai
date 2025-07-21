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

                // ── ignore Z‑ layers ────────────────────────────
                if (ent is Entity e &&
                    e.Layer.StartsWith("Z-", StringComparison.OrdinalIgnoreCase))
                    continue;
                // ────────────────────────────────────────────────

                switch (ent)
                {
                    case DBText t:
                        {
                            string txt = t.TextString;
                            if (IsMostlyNumeric(txt)) break;

                            sw.WriteLine(
                                $"{id.Handle},{nameof(DBText)},\"{txt}\",{t.Layer},{t.Height}");
                            break;
                        }

                    case MText m:
                        {
                            string txt = m.Text;          // duplicates are fine in a new scope
                            if (IsMostlyNumeric(txt)) break;

                            sw.WriteLine(
                                $"{id.Handle},{nameof(MText)},\"{txt}\",{m.Layer},{m.TextHeight}");
                            break;
                        }
                }
            }

            static bool IsMostlyNumeric(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return true;
                int digit = 0, alpha = 0;
                foreach (char c in s)
                {
                    if (char.IsDigit(c)) digit++;
                    else if (char.IsLetter(c)) alpha++;
                }
                return digit > 0 && alpha == 0;
            }
        }
    }
}