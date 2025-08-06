using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadQaPlugin
{
    public class AdvancedSpellCheckRule : RuleBase
    {
        public override string Name => "Advanced SpellCheck";

        private static HashSet<string> AllowedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // built-in tokens
            "PLAN", "P/L", "R/W", "MSL", "LOC", "PLA", "SURFACE", "AMENDED"
        };

        private static HashSet<string> AllowedWidths = new HashSet<string>
        {
            "5.00", "10.00", "12.00", "15.00", "20.00", "25.00", "30.00"
        };

        public override IEnumerable<QaIssue> Evaluate(Database db, Transaction tr)
        {
            // merge built-in and additional tokens from external file
            try
            {
                AllowedTokens.UnionWith(LoadAdditionalTokens());
            }
            catch
            {
                // ignore file errors
            }

            var issues = new List<QaIssue>();

            foreach (ObjectId id in GetModelSpaceEntities(db, tr))
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                string text = null;
                if (ent is DBText dt)
                {
                    text = dt.TextString;
                }
                else if (ent is MText mt)
                {
                    text = mt.Contents;
                }
                else if (ent is Dimension dim)
                {
                    text = dim.DimensionText;
                }
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                // basic cleaning: remove formatting codes etc.
                string cleaned = Clean(text);
                var tokens = Regex.Split(cleaned, @"[\s,;/()\[\]{}]+");
                bool afterDisposition = false;

                foreach (var token in tokens)
                {
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }
                    var tok = token.Trim();

                    // skip numeric tokens following disposition keywords like PLAN/MSL/LOC/PLA
                    if (afterDisposition && IsNumeric(tok))
                    {
                        continue;
                    }

                    if (AllowedTokens.Contains(tok))
                    {
                        // if token is a disposition keyword, next numeric token should be ignored
                        if (string.Equals(tok, "PLAN", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(tok, "MSL", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(tok, "LOC", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(tok, "PLA", StringComparison.OrdinalIgnoreCase))
                        {
                            afterDisposition = true;
                        }
                        continue;
                    }

                    // reset flag if token is not numeric or disposition
                    afterDisposition = false;

                    if (IsNumeric(tok))
                    {
                        // treat numeric tokens as possible width values
                        if (!AllowedWidths.Contains(tok))
                        {
                            issues.Add(new QaIssue
                            {
                                Id = Name,
                                Type = IssueType.Spelling,
                                EntityId = id,
                                Message = $"Unexpected numeric value '{tok}' in entity {id}."
                            });
                        }
                        continue;
                    }

                    // compute fuzzy match to suggest corrections
                    string closest = GetClosestToken(tok, AllowedTokens);
                    int distance = closest == null ? int.MaxValue : LevenshteinDistance(tok.ToUpperInvariant(), closest.ToUpperInvariant());
                    if (distance <= 2)
                    {
                        issues.Add(new QaIssue
                        {
                            Id = Name,
                            Type = IssueType.Spelling,
                            EntityId = id,
                            Message = $"Possible misspelling '{tok}' in entity {id}. Did you mean '{closest}'?"
                        });
                    }
                    else
                    {
                        issues.Add(new QaIssue
                        {
                            Id = Name,
                            Type = IssueType.Spelling,
                            EntityId = id,
                            Message = $"Unrecognized text '{tok}' in entity {id}."
                        });
                    }
                }
            }
            return issues;
        }

        private static bool IsNumeric(string s)
        {
            return double.TryParse(s, out var _);
        }

        private static IEnumerable<string> LoadAdditionalTokens()
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // locate file in the same directory as the assembly
                string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string path = Path.Combine(dir ?? string.Empty, "spell_allowed_tokens.txt");
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadLines(path))
                    {
                        var token = line.Trim();
                        if (!string.IsNullOrEmpty(token))
                        {
                            tokens.Add(token);
                        }
                    }
                }
            }
            catch
            {
                // ignore file errors
            }
            return tokens;
        }

        private static string GetClosestToken(string token, IEnumerable<string> allowed)
        {
            int minDist = int.MaxValue;
            string closest = null;
            foreach (var t in allowed)
            {
                int dist = LevenshteinDistance(token.ToUpperInvariant(), t.ToUpperInvariant());
                if (dist < minDist)
                {
                    minDist = dist;feat: add AdvancedSpellCheckRule with dynamic token loading and fuzzy matching
                    closest = t;
                }
            }
            return closest;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private static string Clean(string text)
        {
            // Here we could remove formatting codes; for now return the original text.
            return text;
        }
    }
}
