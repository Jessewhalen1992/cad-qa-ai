using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

namespace CadQa.Rules
{
    public class AdvancedSpellCheckRule : RuleBase
    {
        public override string Name => "Advanced SpellCheck";

        private static readonly HashSet<string> BuiltInTokens =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "PLAN","P/L","R/W","MSL","LOC","PLA","SURFACE","AMENDED"
            };

        public override IEnumerable<QaIssue> Evaluate(Database db, Transaction tr)
        {
            var allowed = new HashSet<string>(BuiltInTokens, StringComparer.OrdinalIgnoreCase);
            allowed.UnionWith(LoadAdditionalTokens());

            var issues = new List<QaIssue>();

            foreach (var id in GetModelSpace(db, tr))
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;

                string? raw =
                    ent is DBText dt ? dt.TextString :
                    ent is MText mt ? mt.Contents :
                    ent is Dimension d ? d.DimensionText :
                    null;

                if (string.IsNullOrWhiteSpace(raw)) continue;

                // Strip font formatting codes (e.g. \fArial|b0|i1|c0|p34;)
                string cleaned = Clean(raw);
                var tokens = Regex.Split(cleaned, @"[\s,;/()\[\]{}|]+");

                bool afterDisposition = false;

                foreach (string token in tokens)
                {
                    if (string.IsNullOrWhiteSpace(token)) continue;
                    string tok = token.Trim();

                    // Skip tokens beginning with a backslash (formatting codes)
                    if (tok.StartsWith("\\")) continue;

                    // Skip hyphenated numbers (##-##, #-##, ##-#, #-#)
                    if (Regex.IsMatch(tok, @"^\d*-\d+$") || Regex.IsMatch(tok, @"^\d+-\d*$")) continue;

                    // Skip any token containing a digit (numeric values, bearings, codes)
                    if (tok.Any(char.IsDigit)) continue;

                    // Ignore numeric tokens immediately after disposition keywords
                    if (afterDisposition && IsNumeric(tok)) continue;

                    // Known token (case-insensitive)
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

                    // Case-insensitive fuzzy match
                    string? closest = GetClosestToken(tok, allowed);
                    int dist = closest == null ? int.MaxValue :
                        LevenshteinDistance(tok.ToUpperInvariant(), closest.ToUpperInvariant());

                    // Require more than 50% of characters to match: dist < len/2
                    if (closest != null && dist * 2 < tok.Length)
                    {
                        issues.Add(MakeIssue(id,
                            $"Possible misspelling '{tok}'. Did you mean '{closest}'?"));
                    }
                    else
                    {
                        issues.Add(MakeIssue(id,
                            $"Unrecognized text '{tok}'."));
                    }
                }
            }
            return issues;
        }

        private static bool IsNumeric(string s) => double.TryParse(s, out _);

        private static QaIssue MakeIssue(ObjectId id, string msg) =>
            new QaIssue { Type = IssueType.Spelling, EntityId = id, Message = msg };

        private static IEnumerable<string> LoadAdditionalTokens()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var locations = new List<string>();

            // Look for spell_allowed_tokens.txt next to the DLL
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            locations.Add(Path.Combine(dllDir, "spell_allowed_tokens.txt"));

            // Also look in the current drawing’s folder
            try
            {
                string? dwgPath = Application.DocumentManager.MdiActiveDocument?.Database?.Filename;
                if (!string.IsNullOrWhiteSpace(dwgPath))
                {
                    string dwgDir = Path.GetDirectoryName(dwgPath)!;
                    locations.Add(Path.Combine(dwgDir, "spell_allowed_tokens.txt"));
                }
            }
            catch { /* ignore if no drawing */ }

            foreach (var filePath in locations)
            {
                if (File.Exists(filePath))
                {
                    foreach (var line in File.ReadLines(filePath))
                        if (!string.IsNullOrWhiteSpace(line))
                            set.Add(line.Trim());
                    break;
                }
            }
            return set;
        }

        private static string? GetClosestToken(string token, IEnumerable<string> allowed)
        {
            string? closest = null;
            int min = int.MaxValue;
            foreach (var t in allowed)
            {
                int d = LevenshteinDistance(token.ToUpperInvariant(), t.ToUpperInvariant());
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
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                                d[i - 1, j - 1] + cost);
                }
            return d[n, m];
        }

        private static string Clean(string text)
        {
            // Remove MTEXT font specifications (e.g. \fArial|b0|i1|c0|p34;)
            string noFont = Regex.Replace(text, @"\\f[^;]*;", " ");
            // Remove other common formatting codes
            noFont = noFont.Replace("\\P", " ")
                           .Replace("\\A1;", " ")
                           .Replace("\\H0.7;", " ");
            return noFont.Trim();
        }

        private static IEnumerable<ObjectId> GetModelSpace(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId objId in ms) yield return objId;
        }
    }
}
