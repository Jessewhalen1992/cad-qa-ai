using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.IO;

namespace CadQa.Export
{
    public static class ExportFeatures
    {
        public static void DumpFeatures(Database db, Transaction tr, string csvPath)
        {
            using var sw = new StreamWriter(csvPath);
            sw.WriteLine("Handle,ObjType,Content,Layer,Extra");

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);

                // skip Z‑ layers
                if (ent is Entity e &&
                    e.Layer.StartsWith("Z-", StringComparison.OrdinalIgnoreCase))
                    continue;

                switch (ent)
                {
                    // TEXT ---------------------------------------------------------
                    case DBText t:
                        {
                            string txt = Clean(t.TextString);
                            if (IsMostlyNumeric(txt)) break;

                            sw.WriteLine(
                                $"{id.Handle},{nameof(DBText)},\"{txt}\",{t.Layer},{t.Height}");
                            break;
                        }

                    case MText m:
                        {
                            string txt = Clean(m.Text);
                            if (IsMostlyNumeric(txt)) break;

                            sw.WriteLine(
                                $"{id.Handle},{nameof(MText)},\"{txt}\",{m.Layer},{m.TextHeight}");
                            break;
                        }

                    // BLOCK --------------------------------------------------------
                    case BlockReference br:
                        {
                            sw.WriteLine(
                                $"{id.Handle},{nameof(BlockReference)},\"{br.Name}\",{br.Layer},{br.ScaleFactors.X}");
                            break;
                        }

                    // DIMENSION ----------------------------------------------------
                    case Dimension dim:
                        {
                            string txt = Clean(dim.DimensionText?.Trim());
                            if (IsMostlyNumeric(txt)) break;

                            sw.WriteLine(
                                $"{id.Handle},{dim.GetType().Name},\"{txt}\",{dim.Layer},{dim.DimensionStyleName}");
                            break;
                        }

                    // LEADER -------------------------------------------------------
                    case Leader l:
                        {
                            // safely resolve annotation text
                            string txt = "";
                            if (!l.Annotation.IsNull)
                            {
                                var obj = tr.GetObject(l.Annotation, OpenMode.ForRead, false);
                                if (obj is MText mAnnot)
                                    txt = Clean(mAnnot.Text);
                            }

                            if (IsMostlyNumeric(txt)) break;

                            sw.WriteLine(
                                $"{id.Handle},{nameof(Leader)},\"{txt}\",{l.Layer},");
                            break;
                        }

                    case MLeader ml:
                        {
                            string txt = Clean(ml.MText?.Text ?? "");
                            if (IsMostlyNumeric(txt)) break;

                            sw.WriteLine(
                                $"{id.Handle},{nameof(MLeader)},\"{txt}\",{ml.Layer},");
                            break;
                        }
                }
            }
        }

        private static bool IsMostlyNumeric(string s)
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

        private static string Clean(string s) =>
            s?.Replace("\r\n", " ").Replace("\n", " ") ?? "";
    }
}
