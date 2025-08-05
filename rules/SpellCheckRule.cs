using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadQa.Rules
{
    public class SpellCheckRule : RuleBase
    {
        public override string Name => "SpellCheck";

        private static readonly HashSet<string> AllowedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PLAN",
            "P/L",
            "R/W",
            "MSL",
            "LOC",
            "PLA",
            "SURFACE",
            "TO",
            "BE",
            "AMENDED",
            "A/R",
            "AR",
            "VEREN",
            "CNRL",
            "PEMBINA",
            "HWN"
        };

        private static readonly HashSet<string> AllowedWidths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "10.00",
            "20.00",
            "15.00",
            "5.00",
            "12.00",
            "25.00",
            "30.00"
        };

        public override IEnumerable<QaIssue> Evaluate(Database db, Transaction tr)
        {
            var issues = new List<QaIssue>();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent))
                    continue;

                var layer = ent.Layer ?? "";
                if (layer.StartsWith("Z-", StringComparison.OrdinalIgnoreCase) ||
                    layer.Equals("DEFPOINTS", StringComparison.OrdinalIgnoreCase) ||
                    layer.Equals("VIEWPORTS", StringComparison.OrdinalIgnoreCase) ||
                    layer.Equals("0", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? text = null;
                if (ent is DBText dt)
                    text = dt.TextString;
                else if (ent is MText mt)
                    text = mt.Text;
                else if (ent is Dimension dim)
                    text = dim.DimensionText;

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var tokens = Regex.Split(text.Trim(), @"[\s,;/()\[\]{}]+");
                bool ignoreNextNumeric = false;
                foreach (var rawTok in tokens)
                {
                    if (string.IsNullOrWhiteSpace(rawTok))
                        continue;
                    var tok = rawTok.Trim();

                    if (tok.Equals("PLAN", StringComparison.OrdinalIgnoreCase))
                    {
                        ignoreNextNumeric = true;
                        continue;
                    }

                    if (AllowedTokens.Contains(tok))
                    {
                        // For dispositions like MSL or LOC etc, ignore the next numeric token
                        if (tok.Equals("MSL", StringComparison.OrdinalIgnoreCase) ||
                            tok.Equals("LOC", StringComparison.OrdinalIgnoreCase) ||
                            tok.Equals("PLA", StringComparison.OrdinalIgnoreCase))
                        {
                            ignoreNextNumeric = true;
                        }
                        continue;
                    }

                    bool isNumeric = tok.All(c => char.IsDigit(c) || c == '.' || c == '-');
                    if (ignoreNextNumeric && isNumeric)
                    {
                        ignoreNextNumeric = false;
                        continue;
                    }
                    ignoreNextNumeric = false;

                    if (isNumeric)
                    {
                        if (!AllowedWidths.Contains(tok))
                        {
                            issues.Add(new QaIssue
                            {
                                Type = IssueType.Spelling,
                                EntityId = id,
                                Message = $"Unexpected numeric value '{tok}' in entity."
                            });
                        }
                        continue;
                    }

                    if (!AllowedTokens.Contains(tok))
                    {
                        issues.Add(new QaIssue
                        {
                            Type = IssueType.Spelling,
                            EntityId = id,
                            Message = $"Unrecognized text '{tok}' in entity."
                        });
                    }
                }
            }

            return issues;
        }
    }
}
