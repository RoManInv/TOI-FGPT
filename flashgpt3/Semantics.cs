using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.ProgramSynthesis.Transformation.Formula.Semantics.Extensions;
using Microsoft.ProgramSynthesis.Utils;
using Newtonsoft.Json;

namespace FlashGPT3
{
    public static class Semantics
    {

        /// <summary>
        /// Indicate whether we are in learning or evaluation mode. In learning
        /// mode, will consider the model to be an oracle. In evaluation mode,
        /// actually try to call GPT3.
        /// </summary>
        internal static bool learning = false;
        internal static int learningCalls = 0;

        // Concatenate two strings
        public static string Concat(string a, string b)
        {
            return a + b;
        }

        // Substring
        public static string SubStr(string v, int start, int end)
        {
            if (start < 0 || end < 0 || end > v.Length || end < start)
                return null;
            return v[start..end];
        }

        // Constant string
        public static string ConstStr(string s)
        {
            return s;
        }

        // Absolute position
        public static int? AbsPos(string v, int k)
        {
            if (Math.Abs(k) > v.Length + 1)
                return null;
            return k >= 0 ? k : v.Length + k + 1;
        }

        // Regular expression position. Copied from PROSE website.
        public static int? RegPos(string v, Regex left, Regex right, int k)
        {
            if (left == null || right == null)
                return null;
            var rightMatches = right.Matches(v)
                                    .Cast<Match>()
                                    .ToDictionary(m => m.Index);
            var matchPositions = new List<int>();
            foreach (Match m in left.Matches(v))
            {
                if (rightMatches.ContainsKey(m.Index + m.Length))
                    matchPositions.Add(m.Index + m.Length);
            }
            if (k >= matchPositions.Count || k < -matchPositions.Count)
                return null;
            return k >= 0 ? matchPositions[k] : matchPositions[matchPositions.Count + k];
        }

        // Semantic position
        public static int? SemPos(string v, Tuple<string, string>[] q, string m)
        {
            string location = "";
            // in learning mode, emulate oracle on known data
            if (learning)
            {
                learningCalls += 1;
                foreach (Tuple<string, string> pair in q)
                {
                    string x = pair.Item1;
                    string y = pair.Item2;
                    if (v == x)
                        location = y;
                }
            }
            // else, run query
            else
                location = OpenAIQueryRunner.Run(q, v, forceInput: true);
            // failed
            if (location == "" || location == null)
                return null;
            // find position in string and return left or right
            Match p = Regex.Match(v, @"\b" + Regex.Escape(location) + @"\b");
            if (!p.Success)
                p = Regex.Match(v, @"\b" + Regex.Escape(location) + @"\b", RegexOptions.IgnoreCase);
            if (m == "L")
                return p.Index;
            else
                return p.Index + location.Length;
        }

        // Semantic map
        public static string SemMap(string v, Tuple<string, string>[] q)
        {

            const string TOICacheFile = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> TOICache = LoadCache(TOICacheFile);

            TOIIdentSim toi = new TOIIdentSim();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Restart();
            if (learning)
            {
                learningCalls += 1;
                foreach (Tuple<string, string> pair in q)
                {
                    string x = pair.Item1;
                    string y = pair.Item2;
                    if (v == x)
                    {
                        stopwatch.Stop();
                        return y;
                    }
                }
                return null;
            }
            else
            {
                List<Tuple<string, string>> toiq = new List<Tuple<string, string>>();
                foreach (Tuple<string, string> pair in q)
                {
                    string origx = pair.Item1;
                    string origy = pair.Item2;
                    string toix = null;
                    string toiy = null;
                    Tuple<string, string> currtoi = null;
                    if (pair.Item1 != null && TOICache.Keys.Contains(pair.Item1))
                    {
                        currtoi = TOICache[pair.Item1];
                    }
                    if (!(currtoi is null) &&
                       !currtoi.Item1.IsNullOrEmpty() && !currtoi.Item2.IsNullOrEmpty() &&
                       pair.Item2.Equals(currtoi.Item2))
                        toiq.Add(new Tuple<string, string>(currtoi.Item1, currtoi.Item2));
                    else 
                        toiq.Add(new Tuple<string, string>(pair.Item1, pair.Item2));
                }


                Tuple<string, string>[] toiq_array = toiq.ToArray();
                var res = OpenAIQueryRunner.Run(toiq_array, v, forceInput: false).Trim();
                
                stopwatch.Stop();


                return res;
            }
        }


        public static Dictionary<string, Tuple<string, string>> LoadCache(string file)
        {
            Dictionary<string, Tuple<string, string>> _cache = new Dictionary<string, Tuple<string, string>>();
                
            // load from JSON file
            if (File.Exists(file))
            {
                _cache =
                    JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(
                        File.ReadAllText(file)
                    );
            }
            return _cache;
        }

        /// <summary>
        /// Save the cache.
        /// </summary>
        /// <param name="file"></param>
        public static void SaveCache(string file, dynamic _cache)
        {
            if (!Directory.GetParent(file).Exists)
                Directory.GetParent(file).Create();
            File.WriteAllText(file, JsonConvert.SerializeObject(_cache,
                                                                Formatting.Indented));
        }
    }
}
