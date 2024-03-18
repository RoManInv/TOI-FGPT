using AngleSharp.Dom;
using Microsoft.ProgramSynthesis.Extraction.Web.Build.RuleNodeTypes;
using Microsoft.ProgramSynthesis.Transformation.Formula.Semantics.Extensions;
using Microsoft.ProgramSynthesis.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace FlashGPT3
{
    internal class ToiStructure
    {
        public Tuple<string, string> Left { get; set; }
        public Tuple<string, string> Toi { get; set; }
        public Tuple<string, string> Right { get; set; }
    }

    internal static partial class ClusteringUtils
    {


        internal static List<List<string>> ClusterGreedy_TOI(List<Tuple<string, List<string>>> options_tuple, bool print = false)
        {

            const string TOICache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(TOICache));

            List<List<string>> options = new();


            foreach (var xys in options_tuple)
            {
                List<string> candidates = new List<string>();
                Tuple<string, string> currtoi = toiDict[xys.Item1];
                bool flag = false;
                if (xys.Item2.All(s => xys.Item1.Contains(s)))
                {
                    flag = false;
                }
                else
                {
                    flag = true;
                }
                List<string> currtoi_candidates_forward;
                List<string> currtoi_candidates_backward;
                if (flag)
                {
                    currtoi_candidates_forward = StringUtils.GetAllSubstringForward(currtoi.Item2);
                    currtoi_candidates_backward = StringUtils.GetAllSubStringBackward(currtoi.Item2);
                }
                else
                {
                    currtoi_candidates_forward = StringUtils.GetAllSubstringForward(currtoi.Item1);
                    currtoi_candidates_backward = StringUtils.GetAllSubStringBackward(currtoi.Item1);
                }
                bool TOIHit = false;
                string prevY = null;
                bool fwd_EndswFwd = false;
                bool bwd_StartswBwdEndswBwd = false;
                bool bwd_StartswBwdNEndswBwd = false;

                foreach (string y in xys.Item2)
                {
                    if (y.EndsWith(currtoi.Item2) || (y.EndsWith(currtoi.Item1) && !y.EndsWith(currtoi.Item2) && currtoi.Item1.Contains(y))) { candidates.Add(y); TOIHit = false; }
                    else if (!currtoi_candidates_forward.Any(s => y.EndsWith(s)) && !currtoi_candidates_backward.Any(s => y.StartsWith(s)) &&
                        !((currtoi_candidates_backward.Any(s => y.StartsWith(s)) && currtoi_candidates_backward.Any(s => y.EndsWith(s)))))
                    {
                        if (!TOIHit) candidates.Add(y);
                        else
                        {
                            candidates.Add(prevY);
                            candidates.Add(y);
                            TOIHit = false;
                        }
                    }
                    else
                    {
                        TOIHit = true;
                        prevY = y;
                    }



                }
                if (TOIHit && !prevY.IsNullOrEmpty())
                {
                    candidates.Add(prevY);
                }
                options.Add(candidates);
            }



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
    }
}
