using Microsoft.ProgramSynthesis.Transformation.Formula.Build.RuleNodeTypes;
using Microsoft.ProgramSynthesis.Transformation.Formula.Semantics.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlashGPT3
{
    static class RegexUtils
    {
        public static readonly Regex[] Tokens =
        {
            new Regex(@"", RegexOptions.Compiled), // Epsilon
            new Regex(@"\p{Lu}(\p{Ll})+", RegexOptions.Compiled), // Camel Case
            new Regex(@"\p{Ll}+", RegexOptions.Compiled), // Lowercase word
            new Regex(@"\p{Lu}(\p{Lu})+", RegexOptions.Compiled), // Uppercase word
            new Regex(@"[0-9]+(\,[0-9]{3})*(\.[0-9]+)?", RegexOptions.Compiled), // Number
            new Regex(@"\p{Zs}+", RegexOptions.Compiled), // WhiteSpace
            new Regex(@"\t", RegexOptions.Compiled), // Tab
            new Regex(@",", RegexOptions.Compiled), // Comma
            new Regex(@"\.", RegexOptions.Compiled), // Dot
            new Regex(@":", RegexOptions.Compiled), // Colon
            new Regex(@";", RegexOptions.Compiled), // Semicolon
            new Regex(@"!", RegexOptions.Compiled), // Exclamation
            new Regex(@"\)", RegexOptions.Compiled), // Right Parenthesis
            new Regex(@"\(", RegexOptions.Compiled), // Left Parenthesis
            new Regex(@"""", RegexOptions.Compiled), // Double Quote
            new Regex(@"'", RegexOptions.Compiled), // Single Quote
            new Regex(@"/", RegexOptions.Compiled), // Forward Slash
            new Regex(@"\\", RegexOptions.Compiled), // Backward Slash
            new Regex(@"-", RegexOptions.Compiled), // Hyphen
            new Regex(@"\*", RegexOptions.Compiled), // Star
            new Regex(@"\+", RegexOptions.Compiled), // Plus
            new Regex(@"_", RegexOptions.Compiled), // Underscore
            new Regex(@"=", RegexOptions.Compiled), // Equal
            new Regex(@">", RegexOptions.Compiled), // Greater-than
            new Regex(@"<", RegexOptions.Compiled), // Left-than
            new Regex(@"\]", RegexOptions.Compiled), // Right Bracket
            new Regex(@"\[", RegexOptions.Compiled), // Left Bracket
            new Regex(@"}", RegexOptions.Compiled), // Right Brace
            new Regex(@"{", RegexOptions.Compiled), // Left Brace
            new Regex(@"\|", RegexOptions.Compiled), // Bar
            new Regex(@"&", RegexOptions.Compiled), // Ampersand
            new Regex(@"#", RegexOptions.Compiled), // Hash
            new Regex(@"\$", RegexOptions.Compiled), // Dollar
            new Regex(@"\^", RegexOptions.Compiled), // Hat
            new Regex(@"@", RegexOptions.Compiled), // At
            new Regex(@"%", RegexOptions.Compiled), // Percentage
            new Regex(@"\?", RegexOptions.Compiled), // Question Mark
            new Regex(@"~", RegexOptions.Compiled), // Tilde
            new Regex(@"`", RegexOptions.Compiled), // Back Prime
            new Regex(@"\u2192", RegexOptions.Compiled), // RightArrow
            new Regex(@"\u2190", RegexOptions.Compiled), // LeftArrow
        };

        /* 
         * Get all interesting substrings in a direction.
         * 
         * Only consider letters, as we only require them in
         * out use cases. Could be easily extended to other
         * characters as well, see interestingRegexes below.
         */
        public static readonly Regex rInterestingRegex = new Regex("(?<=(^|[^a-zA-Z0-9]))[a-zA-Z0-9]", RegexOptions.Compiled);
        public static readonly Regex lInterestingRegex = new Regex("(?<=[a-zA-Z0-9])([^a-zA-Z0-9]|$)", RegexOptions.Compiled);

        internal static List<string> GetAllSubstringForward(string str)
        {
            if (str.IsNullOrEmpty())
            {
                return new List<string>();
            }
            List<string> substrings = new List<string>();

            for (int i = 1; i <= str.Length - 1; i++)
            {
                substrings.Add(str.Substring(0, i));
            }

            return substrings;
        }

        // (smaller/smallest)

        internal static List<string> GetAllSubStringBackward(string str)
        {
            if (str.IsNullOrEmpty())
            {
                return new List<string>();
            }
            List<string> substrings = new List<string>();

            for (int i = str.Length - 1; i > 0; i--)
            {
                substrings.Add(str.Substring(str.Length - i));
            }

            return substrings;
        }
        public static IEnumerable<string> DirectedInterestingStrings(string s, string d)
        {
            IEnumerable<string> res = (d == "L") ? lInterestingRegex.Matches(s)
                                                 .Cast<System.Text.RegularExpressions.Match>()
                                                 .Select(m => m.Index)
                                                 .Select(m => s.Substring(0, m)) :
                                                   rInterestingRegex.Matches(s)
                                                 .Cast<System.Text.RegularExpressions.Match>()
                                                 .Select(m => m.Index)
                                                 .Select(m => s.Substring(m));
            return res;
        }

        public static IEnumerable<string> DirectedInterestingStrings(string s, string d, Tuple<string, string> toi)
        {
            List<string> res = new List<string>();
            bool TOIHit = false;
            string prev = null;
            if (d == "L")
            {
                IEnumerable<string> strings = lInterestingRegex.Matches(s)
                                                .Cast<System.Text.RegularExpressions.Match>()
                                                .Select(m => m.Index)
                                                .Select(m => s.Substring(0, m));
                List<string> TOIForward = StringUtils.GetAllSubstringForward(toi.Item2);
                foreach (string str in strings)
                {
                    if (!TOIForward.Any(s => str.StartsWith(s)))
                    {
                        if (!TOIHit) res.Add(str);
                        else
                        {
                            res.Add(prev);
                            TOIHit = false;
                        }
                    }
                    else
                    {
                        TOIHit = true;
                        prev = str;
                    }
                }
            }
            else
            {
                IEnumerable<string> strings = rInterestingRegex.Matches(s)
                                                .Cast<System.Text.RegularExpressions.Match>()
                                                .Select(m => m.Index)
                                                .Select(m => s.Substring(0, m));
                List<string> TOIForward = StringUtils.GetAllSubstringForward(toi.Item2);
                foreach (string str in strings)
                {
                    if (!TOIForward.Any(s => str.EndsWith(s)))
                    {
                        if (!TOIHit) res.Add(str);
                        else
                        {
                            res.Add(prev);
                            TOIHit = false;
                        }
                    }
                    else
                    {
                        TOIHit = true;
                        prev = str;
                    }
                }
            }
            return res;
        }

        /*
         * Get all interesting strings.
         */
        public static readonly Regex[] classRegexes =
        {
            new Regex(@"[a-zA-Z]", RegexOptions.Compiled),
            new Regex(@"[0-9]", RegexOptions.Compiled),
            new Regex(@"[\s]", RegexOptions.Compiled),
        };
        public static IEnumerable<string> InterestingStrings(string s)
        {
            List<string> strings = new();
            for (int i = 1; i < s.Length; i++)
            {
                string a = s[i - 1].ToString();
                string b = s[i].ToString();
                if (!(a == b ||
                      classRegexes.Select(r => r.IsMatch(a) &&
                      r.IsMatch(b)).Contains(true)))
                    strings.Add(s.Substring(0, i));
            }
            return strings.Distinct();
        }

        public static Dictionary<string, List<Tuple<System.Text.RegularExpressions.Match, Regex>>[]> lCache = new();
        public static Dictionary<string, List<Tuple<System.Text.RegularExpressions.Match, Regex>>[]> rCache = new();
        public static void BuildStringMatches(string x,
                                              out List<Tuple<System.Text.RegularExpressions.Match, Regex>>[] leftMatches,
                                              out List<Tuple<System.Text.RegularExpressions.Match, Regex>>[] rightMatches)
        {
            // If cached, assume both are cached.
            if (lCache.ContainsKey(x))
            {
                leftMatches = lCache[x];
                rightMatches = rCache[x];
            }
            // Else, compute and store in cache.
            else
            {
                leftMatches = new List<Tuple<System.Text.RegularExpressions.Match, Regex>>[x.Length + 1];
                rightMatches = new List<Tuple<System.Text.RegularExpressions.Match, Regex>>[x.Length + 1];
                for (int p = 0; p <= x.Length; ++p)
                {
                    leftMatches[p] = new List<Tuple<System.Text.RegularExpressions.Match, Regex>>();
                    rightMatches[p] = new List<Tuple<System.Text.RegularExpressions.Match, Regex>>();
                }
                foreach (Regex r in Tokens)
                {
                    foreach (System.Text.RegularExpressions.Match m in r.Matches(x))
                    {
                        leftMatches[m.Index + m.Length].Add(Tuple.Create(m, r));
                        rightMatches[m.Index].Add(Tuple.Create(m, r));
                    }
                }
                lCache[x] = leftMatches;
                rCache[x] = rightMatches;
            }
        }

        // Not regex, but still pretty helpful.
        // Source: https://stackoverflow.com/a/2641383/3350448
        public static List<int> AllIndexesOf(this string str, string value)
        {
            if (String.IsNullOrEmpty(value))
                throw new ArgumentException("the string to find may not be empty", "value");
            List<int> indexes = new();
            for (int index = 0; ; index += value.Length)
            {
                index = str.IndexOf(value, index);
                if (index == -1)
                    return indexes;
                indexes.Add(index);
            }
        }

    }
}
