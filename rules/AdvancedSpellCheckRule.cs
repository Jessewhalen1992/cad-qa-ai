using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CadQa.Rules   // same namespace as RuleBase / BlockLayerRule
{
    public class AdvancedSpellCheckRule : RuleBase
    {
        public override string Name => "Advanced SpellCheck";

        // ---------------- Allowed tokens & widths -------------------------
        private static readonly HashSet<string> BuiltInTokens =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "PLAN", "P/L", "R/W", "MSL", "LOC", "PLA",
                "SURFACE", "AMENDED"
            };

        private static readonly HashSet<string> AllowedWidths =
            new() { "5.00", "10.00", "12.00", "15.00",
                    "20.00", "25.00", "30.00" };
        // ------------------------------------------------------------------

        public override IEnumerable<QaIssue> Evaluate(Database db, Transaction tr)
        {
            var allowed = new HashSet<string>(BuiltInTokens, StringComparer.OrdinalIgnoreCase);
            allowed.UnionWith(LoadAdditionalTokens());

            var issues = new List<QaIssue>();

            foreach (ObjectId id in GetModelSpace(db, tr))   // helper below
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;

                string? raw =
                    ent is DBText dt ? dt.TextString :
                    ent is MText mt ? mt.Contents :
                    ent is Dimension d ? d.DimensionText :
                    null;

                if (string.IsNullOrWhiteSpace(raw)) continue;

                string cleaned = Clean(raw);
                var tokens = Regex.Split(cleaned, @"[\s,;/()\[\]{}]+");

                bool afterDisposition = false;

                foreach (string token in tokens)
                {
                    if (string.IsNullOrWhiteSpace(token)) continue;
                    string tok = token.Trim();

                    // skip numeric token right after disposition keywords
                    if (afterDisposition && IsNumeric(tok)) continue;

                    if (allowed.Contains(tok))
                    {
                        if (tok.Equals("PLAN", StringComparison.OrdinalIgnoreCase) ||
                            tok.Equals("MSL", StringComparison.OrdinalIgnoreCase) ||
                            tok.Equals("LOC", StringComparison.OrdinalIgnoreCase) ||
                            tok.Equals("PLA", StringComparison.OrdinalIgnoreCase))
                            afterDisposition = true;
                        continue;
                    }

                    afterDisposition = false;

                    // numeric width check
                    if (IsNumeric(tok))
                    {
                        if (!AllowedWidths.Contains(tok))
                            issues.Add(MakeIssue(id, $"Unexpected numeric value '{tok}'."));
                        continue;
                    }

                    // fuzzy match
                    string? closest = GetClosestToken(tok, allowed);
                    int dist = closest == null ? int.MaxValue
                                               : LevenshteinDistance(tok, closest);

                    if (closest != null && dist <= 2)
                        issues.Add(MakeIssue(id,
                            $"Possible misspelling '{tok}'. Did you mean '{closest}'?"));
                    else
                        issues.Add(MakeIssue(id, $"Unrecognized text '{tok}'."));
                }
            }

            return issues;
        }

        // ---------------- Helpers -----------------------------------------

        private static bool IsNumeric(string s) => double.TryParse(s, out _);

        private static QaIssue MakeIssue(ObjectId id, string msg) =>
            new QaIssue
            {
                // Id left unset (or set to an int if you prefer, e.g. Id = 3,)
                Type = IssueType.Spelling,
                EntityId = id,
                Message = msg
            };

        private static IEnumerable<string> LoadAdditionalTokens()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string dir = Path.GetDirectoryName(
                                System.Reflection.Assembly.GetExecutingAssembly().Location)!;
                string path = Path.Combine(dir, "spell_allowed_tokens.txt");
                if (File.Exists(path))
                    foreach (string line in File.ReadLines(path))
                        if (!string.IsNullOrWhiteSpace(line))
                            set.Add(line.Trim());
            }
            catch { /* ignore I/O errors */ }
            return set;
        }

        private static string? GetClosestToken(string token, IEnumerable<string> allowed)
        {
            string? closest = null;
            int min = int.MaxValue;

            foreach (string t in allowed)
            {
                int d = LevenshteinDistance(token, t);
                if (d < min)
                {
                    min = d;
                    closest = t;
                }
            }
            return closest;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                                  Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                                  d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private static string Clean(string text) =>
            text.Replace("\\P", " ")
                .Replace("\\A1;", " ")
                .Replace("\\H0.7;", " ")
                .Trim();

        private static IEnumerable<ObjectId> GetModelSpace(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms) yield return id;
        }
    }
}
