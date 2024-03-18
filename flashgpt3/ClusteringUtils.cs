using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using F23.StringSimilarity;
using F23.StringSimilarity.Interfaces;
using MathNet.Numerics.Distributions;
using Microsoft.ProgramSynthesis.Utils;
using Microsoft.ProgramSynthesis.Wrangling.Schema.TableOutput;
using Newtonsoft.Json;

namespace FlashGPT3
{
    internal static partial class ClusteringUtils
    {
        private static string ToStringExt<T>(List<T> list) => "[" + string.Join(", ", list) + "]";
        private static string ToStringExt<T>(List<List<T>> listOfLists) => "[" + string.Join(", ", listOfLists.Select(l => ToStringExt(l))) + "]";

        public delegate double StringSimilarity(string a, string b);
        public static StringSimilarity Similarity = TokenCountSimilarity;

        public static readonly Regex[] Tokens =
        {
            new Regex(@"\b\p{Lu}(\p{Ll})+\b", RegexOptions.Compiled), // Camel Case
            new Regex(@"\b\p{Ll}+\b", RegexOptions.Compiled), // Lowercase word
            new Regex(@"\b\p{Lu}(\p{Lu})+\b", RegexOptions.Compiled), // Uppercase word
            new Regex(@"\b[0-9]+(\,[0-9]{3})*(\.[0-9]+)?\b", RegexOptions.Compiled), // Number
            new Regex(@" ", RegexOptions.Compiled), // Space
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

        internal static List<List<string>> ClusterGreedy2(List<List<string>> options)
        {
            // get shortest
            int length = options.Min(l => l.Count);
            var shortest = options.FirstOrDefault(l => l.Count == length);
            // remove shortest
            options.Remove(shortest);
            // initialise clusters
            var clusters = new List<List<string>>(length);
            foreach (string s in shortest)
                clusters.Add(new List<string> { s });
            // add rest
            foreach (List<string> row in options)
            {
                // compute list of similarities of cluster to strings
                List<List<double>> similarities = shortest.Select(
                    a => row.Select(b => Similarity(b, a)).ToList()
                ).ToList();
                // iteratively look for lowest value until all clusters
                // are assigned a value
                for (int i = 0; i < similarities.Count; i++)
                {
                    // get best
                    (int cluster, int option) = ArgMaxArgMax(similarities);
                    // add to cluster
                    clusters[cluster].Add(row[option]);
                    // remove the selected option from the options
                    row.RemoveAt(option);
                    foreach (var sim in similarities)
                        if (sim.Count > option)
                            sim.RemoveAt(option);
                    // remove the selected cluster from possible clusters
                    similarities[cluster] = new List<double> { };
                }
            }
            return clusters;
        }

        internal static List<List<string>> ClusterGreedy(List<List<string>> options, bool print = false)
        {
            if (print)
                Console.WriteLine(ToStringExt(options));
            // get shortest
            int length = options.Max(l => l.Count);
            var shortest = options.FirstOrDefault(l => l.Count == length);
            // remove shortest
            options.Remove(shortest);
            // initialise clusters
            var clusters = new List<List<string>>(length);
            foreach (string s in shortest)
                clusters.Add(new List<string> { s });
            // add rest
            foreach (List<string> row in options)
            {
                // compute list of similarities of cluster to strings
                //List<List<double>> similarities = shortest.Select(
                //    a => row.Select(b => Similarity(b, a)).ToList()
                //).ToList();
                List<List<double>> similarities = shortest.Select(
                    a => row.Select(b => TokenCountSimilarity_MS(b, a)).ToList()
                ).ToList();
                // iteratively look for lowest value until all clusters
                // are assigned a value
                for (int i = 0; i < similarities.Count; i++)
                {
                    // get best
                    (int cluster, int option) = ArgMaxArgMax(similarities);
                    // add to cluster
                    clusters[cluster].Add(row[option]);
                    // remove the selected option from the options
                    //row.RemoveAt(option);
                    //foreach (var sim in similarities)
                    //    if (sim.Count > option)
                    //        sim.RemoveAt(option);
                    // remove the selected cluster from possible clusters
                    similarities[cluster] = new List<double> { };
                }
            }
            if (print)
            {
                Console.WriteLine("--------------------");
                Console.WriteLine(ToStringExt(clusters));
                Console.WriteLine("====================");
            }

            return clusters;
        }

        internal static List<List<string>> ClusterAll(List<List<string>> options)
        {
            IEnumerable<IEnumerable<string>> enumerable = new[] { Enumerable.Empty<string>() };
            foreach (List<string> sequence in options)
            {
                enumerable = from accseq in enumerable
                             from item in sequence
                             select accseq.Append(item);
            }
            return enumerable.Select(x => x.ToList()).ToList();
        }

        
        internal static double TokenCountSimilarity(string a, string b)
        {
            // similarity with respect to TOI should be dominant
            double dominantSim = 2.0;
            const string TOICache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(TOICache));
            List<string> LeftTOIs = toiDict.Values.Select(x => x.Item1).ToList();
            List<string> RightTOIs = toiDict.Values.Select(x => x.Item2).ToList();

            if (RightTOIs.Any(s => a.EndsWith(s)) && RightTOIs.Any(s => b.EndsWith(s))) return dominantSim;
            if (LeftTOIs.Any(s => a.EndsWith(s)) && !RightTOIs.Any(s => a.EndsWith(s)) &&
                LeftTOIs.Any(s => b.EndsWith(s)) && !RightTOIs.Any(s => b.EndsWith(s))) return dominantSim;
            if (LeftTOIs.Any(s => a.Equals(s)) && LeftTOIs.Any(s => b.Equals(s))) return dominantSim * dominantSim;

            // count occurrences of all tokens
            double[] fA = new double[Tokens.Length];
            double[] fB = new double[Tokens.Length];
            for (int i = 0; i < Tokens.Length; i++)
            {
                Regex regex = Tokens[i];
                fA[i] = regex.Matches(a).Count;
                fB[i] = regex.Matches(b).Count;
            }
            // return cosime similarity
            return CosineSimilarity(fA, fB);
        }

        /// <summary>
        /// Compute similarity between strings by counting tokens.
        /// </summary>
        internal static double TokenCountSimilarity_MS(string a, string b)
        {
            // count occurrences of all tokens
            double[] fA = new double[Tokens.Length];
            double[] fB = new double[Tokens.Length];
            for (int i = 0; i < Tokens.Length; i++)
            {
                Regex regex = Tokens[i];
                fA[i] = regex.Matches(a).Count;
                fB[i] = regex.Matches(b).Count;
            }
            // return cosime similarity
            return CosineSimilarity(fA, fB);
        }

        /// <summary>
        /// Compute edit similarity between mapped character sequences.
        /// </summary>
        internal static IStringSimilarity editSimilarity = new NormalizedLevenshtein();
        internal static double CharacterEditSimilarity(string a, string b)
        {
            string aM = Characterize(a);
            string bM = Characterize(b);
            return editSimilarity.Similarity(aM, bM);
        }

        internal static double CosineSimilarity(double[] a, double[] b)
        {
            double AB = 0.0;
            double A = 0.0;
            double B = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                AB += a[i] * b[i];
                A += a[i] * a[i];
                B += b[i] * b[i];
            }
            if (A == 0.0)
                return (B == 0.0) ? 1.0 : 0.0;
            if (B == 0.0)
                return 0.0;
            return AB / (Math.Sqrt(A) * Math.Sqrt(B));
        }

        internal static string Characterize(string s)
        {
            string a = Regex.Replace(s, @"\p{Ll}", "a");
            string b = Regex.Replace(a, @"\p{Lu}", "A");
            string c = Regex.Replace(b, @"[0-9]", "0");
            return c;
        }

        internal static Tuple<int, int> ArgMaxArgMax(List<List<double>> items)
        {
            int best_i = 0, best_j = 0;
            double max = -1;
            for (int i = 0; i < items.Count; i++)
                for (int j = 0; j < items[i].Count; j++)
                    if (items[i][j] > max)
                    { 
                        best_i = i;
                        best_j = j;
                        max = items[i][j];
                    }
            return Tuple.Create(best_i, best_j);
        }

        internal static Tuple<string, string>[] BuildQuery(List<string> cluster,
                                                           Dictionary<string, List<string>> map)
        {
            return map.Select(
                t => Tuple.Create(t.Key, t.Value.Intersect(cluster).FirstOrDefault())
            ).ToArray();
            //return cluster.Select(
            //    s => Tuple.Create(map.FirstOrDefault(v => v.Value.Contains(s)).Key, s)
            //).ToArray();
        }

        private static Dictionary<string, Tuple<string, string>> LoadCache(string file)
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
        private static void SaveCache(string file, dynamic _cache)
        {
            if (!Directory.GetParent(file).Exists)
                Directory.GetParent(file).Create();
            File.WriteAllText(file, JsonConvert.SerializeObject(_cache,
                                                                Formatting.Indented));
        }

    }

}
