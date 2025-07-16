using System.IO;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadQa.Export
{
    public static class ExportFeatures
    {
        public static void DumpText(Database db, Transaction tr, string csvPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Handle,ObjType,TextString,Layer,TextHeight");

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(
                bt[BlockTableRecord.ModelSpace],
                OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent is DBText dbText)
                {
                    sb.AppendLine(
                        $"{dbText.Handle},DBText,\"{dbText.TextString}\",{dbText.Layer},{dbText.Height}");
                }
                else if (ent is MText mtext)
                {
                    sb.AppendLine(
                        $"{mtext.Handle},MText,\"{mtext.Text}\",{mtext.Layer},{mtext.TextHeight}");
                }
            }

            File.WriteAllText(csvPath, sb.ToString());
        }
    }
}
