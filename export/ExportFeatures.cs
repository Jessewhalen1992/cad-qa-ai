using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.IO;
using System.Text.RegularExpressions;

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

                // Skip Z‑* layers and DEFPOINTS
                if (ent is Entity e &&
                    (e.Layer.StartsWith("Z-", StringComparison.OrdinalIgnoreCase) ||
                     e.Layer.Equals("DEFPOINTS", StringComparison.OrdinalIgnoreCase)))
                    continue;

                switch (ent)
                {
                    /* ───────── TEXT ───────── */
                    case DBText t:
                        {
                            string txt = Clean(t.TextString);
                            if (IsStrictlyNumeric(txt)) break;
                            sw.WriteLine(
                                $"{id.Handle},{nameof(DBText)},\"{txt}\",{t.Layer},{t.Height}");
                            break;
                        }

                    case MText m:
                        {
                            string txt = Clean(m.Text);
                            if (IsStrictlyNumeric(txt)) break;
                            sw.WriteLine(
                                $"{id.Handle},{nameof(MText)},\"{txt}\",{m.Layer},{m.TextHeight}");
                            break;
                        }

                    /* ───────── BLOCK ──────── */
                    case BlockReference br:
                        {
                            sw.WriteLine(
                                $"{id.Handle},{nameof(BlockReference)},\"{br.Name}\",{br.Layer},{br.ScaleFactors.X}");
                            break;
                        }

                    /* ─────── DIMENSION ────── */
                    case Dimension dim:
                        {
                            string txt = Clean(dim.DimensionText?.Trim());
                            if (IsStrictlyNumeric(txt)) break;
                            sw.WriteLine(
                                $"{id.Handle},{dim.GetType().Name},\"{txt}\",{dim.Layer},{dim.DimensionStyleName}");
                            break;
                        }

                    /* ──────── LEADER ──────── */
                    case Leader l:
                        {
                            string txt = "";
                            if (!l.Annotation.IsNull &&
                                tr.GetObject(l.Annotation, OpenMode.ForRead, false) is MText mt)
                                txt = Clean(mt.Text);
                            if (IsStrictlyNumeric(txt)) break;
                            sw.WriteLine(
                                $"{id.Handle},{nameof(Leader)},\"{txt}\",{l.Layer},");
                            break;
                        }

                    case MLeader ml:
                        {
                            string txt = Clean(ml.MText?.Text ?? "");
                            if (IsStrictlyNumeric(txt)) break;
                            sw.WriteLine(
                                $"{id.Handle},{nameof(MLeader)},\"{txt}\",{ml.Layer},");
                            break;
                        }
                }
            }
        }

        /* ---------- helpers ---------- */

        // Remove \P, \P7.50, \PEZE, \pxqc;, \fArial… up to next whitespace
        private static readonly Regex _fmt =
            new(@"\\[A-Za-z][^\s]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";

            s = _fmt.Replace(s, " ");                         // strip formatting runs
            s = s.Replace("\r\n", " ").Replace("\n", " ");    // flatten real new‑lines
            return s.Trim();
        }

        // Skip only if the string is purely numeric (digits, dot, minus)
        private static bool IsStrictlyNumeric(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;

            foreach (char c in s)
                if (!char.IsDigit(c) && c != '.' && c != '-')   // allow 123, 45.6, -7
                    return false;

            return true;   // all chars were numeric / dot / minus
        }
    }
}
