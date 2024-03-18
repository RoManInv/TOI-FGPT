using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.ProgramSynthesis;
using Microsoft.ProgramSynthesis.Learning;
using Microsoft.ProgramSynthesis.Rules;
using Microsoft.ProgramSynthesis.Rules.Concepts;
using Microsoft.ProgramSynthesis.Specifications;
using Microsoft.ProgramSynthesis.Transformation.Formula.Build.RuleNodeTypes;
using Microsoft.ProgramSynthesis.Transformation.Formula.Semantics.Extensions;
using Microsoft.ProgramSynthesis.Transformation.Formula.Semantics.Learning.Models;
using Microsoft.ProgramSynthesis.Utils;
using Newtonsoft.Json;

namespace FlashGPT3
{
    public class WitnessFunctions : DomainLearningLogic
    {
        private static string ToStringExt<T>(List<T> list) => "[" + string.Join(", ", list) + "]";
        private static string ToStringExt<K, V>(KeyValuePair<K, V> kvp) => string.Format("[{0}] => {1}", kvp.Key, kvp.Value);
        public static string ToStringExt<K, V>(Dictionary<K, V> dic) => "{" + string.Join(", ", dic.Select((kvp) => ToStringExt(kvp))) + "}";

        internal List<string> GetValidSlices(string src, List<String> inputs)
        {
            const string toicache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(toicache));

            Tuple<string, string> toi = toiDict[src];
            List<string> currtoi_candidates_forward = StringUtils.GetAllSubstringForward(toi.Item2);
            List<string> currtoi_candidates_backward = StringUtils.GetAllSubStringBackward(toi.Item2);
            bool TOIHit = false;
            string prevY = null;

            List<string> strings = new List<string>();
            string firstinput = inputs[0];

            if (firstinput.EndsWith(toi.Item1) && !firstinput.Equals(toi.Item1))
            {
                int index = firstinput.IndexOf(toi.Item1);
                string stringbeforefirst = (index < 0)
                    ? firstinput
                    : firstinput.Remove(index, toi.Item1.Length);
                if (!currtoi_candidates_forward.Any(s => stringbeforefirst.StartsWith(s)))
                {
                    if (stringbeforefirst.EndsWith(" "))
                        strings.Add(stringbeforefirst.TrimEnd());
                    strings.Add(stringbeforefirst);
                }
                    
            }


            foreach (string input in inputs)
            {
                string currinput = input;
                if (currinput.Length == 0) { continue; }
                if (!currtoi_candidates_forward.Any(s => currinput.EndsWith(s)) &&
                    !((currtoi_candidates_backward.Any(s => currinput.StartsWith(s)) && currtoi_candidates_backward.Any(s => input.EndsWith(s)))))
                {
                    if (!TOIHit && !strings.Contains(currinput) && !currinput.IsNullOrEmpty()) strings.Add(currinput);
                    else
                    {
                        if (!strings.Contains(prevY) && !prevY.IsNullOrEmpty())
                        {
                            strings.Add(prevY);
                            strings.Add(currinput);
                        }
                        TOIHit = false;
                    }
                }
                else
                {
                    TOIHit = true;
                    prevY = currinput;
                }
            }
            if (TOIHit && !prevY.IsNullOrEmpty() && !strings.Contains(prevY))
            {
                if (prevY.EndsWith(" "))
                {
                    strings.Add(prevY.TrimEnd());
                    strings.Add(prevY);
                }
                else if (prevY.StartsWith(" "))
                {
                    strings.Add(prevY.TrimStart());
                    strings.Add(prevY);
                }
                else strings.Add(prevY);
            }
            return strings;

        }

        internal List<string> GetValidSlices_SemPos(string src, List<String> inputs)
        {
            const string toicache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(toicache));

            Tuple<string, string> toi = toiDict[src];
            List<string> currtoi_candidates_forward = StringUtils.GetAllSubstringForward(toi.Item2);
            List<string> currtoi_candidates_backward = StringUtils.GetAllSubStringBackward(toi.Item2);
            bool TOIHit = false;
            string prevY = null;

            List<string> strings = new List<string>();
            string firstinput = inputs[0];

            if (firstinput.EndsWith(toi.Item1) && !firstinput.Equals(toi.Item1))
            {
                int index = firstinput.IndexOf(toi.Item1);
                string stringbeforefirst = (index < 0)
                    ? firstinput
                    : firstinput.Remove(index, toi.Item1.Length);
                strings.Add(stringbeforefirst);
            }

            string directafterTOI = src.Substring(src.IndexOf(toi.Item1) + toi.Item1.Length);
            if (firstinput.Equals(directafterTOI))
            {
                strings.Add(toi.Item1 + firstinput);
            }


            foreach (string input in inputs)
            {
                string currinput = input;
                if (currinput.Length == 0) { continue; }
                if (!currtoi_candidates_forward.Any(s => currinput.EndsWith(s)) &&
                    !((currtoi_candidates_backward.Any(s => currinput.StartsWith(s)) && currtoi_candidates_backward.Any(s => input.EndsWith(s)))))
                {
                    if (!TOIHit && !strings.Contains(currinput) && !currinput.IsNullOrEmpty()) strings.Add(currinput);
                    else
                    {
                        if (!strings.Contains(prevY) && !prevY.IsNullOrEmpty())
                        {
                            strings.Add(prevY);
                            strings.Add(currinput);
                        }
                        TOIHit = false;
                    }
                }
                else
                {
                    TOIHit = true;
                    prevY = currinput;
                }
            }
            if (TOIHit && !prevY.IsNullOrEmpty() && !strings.Contains(prevY))
            {
                strings.Add(prevY);
            }
            return strings;

        }

        public static bool Clustering = true;

        public WitnessFunctions(Grammar grammar) : base(grammar) { }

        /// <summary>
        /// Witness for first string of concat.
        ///
        /// This version only returns the longest contiguous
        /// substrings of the input.
        /// </summary>
        /// <param name="rule"></param>
        /// <param name="spec"></param>
        /// <returns></returns>
        [WitnessFunction(nameof(Semantics.Concat), 0)]
        internal DisjunctiveExamplesSpec WitnessStringOne(GrammarRule rule,
                                                          DisjunctiveExamplesSpec spec)
        {
            const string toicache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(toicache));
            if (spec.ProvidedInputs.All(input => (!toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item1.IsNullOrEmpty()) &&
                                                  !toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item2.IsNullOrEmpty()))
            {
                var ex = new Dictionary<State, IEnumerable<object>>();
                foreach (State input in spec.ProvidedInputs)
                {
                    string v = (string)input.Bindings.Select(p => p.Value)
                                                  .FirstOrDefault();
                    List<string> tempstrs = new List<string>();
                    foreach (string str in spec.DisjunctiveExamples[input])
                    {
                        tempstrs.Add(str);
                    }
                    tempstrs = GetValidSlices(v, tempstrs);
                    ex[input] = tempstrs;
                }
                spec = DisjunctiveExamplesSpec.From(ex);
            }

            var examples = new Dictionary<State, IEnumerable<object>>();
            foreach (State input in spec.ProvidedInputs)
            {
                var v = (string)input.Bindings.Select(p => p.Value)
                                              .FirstOrDefault();
                // gather the interesting strings
                var interesting = new List<object>();
                foreach (string output in spec.DisjunctiveExamples[input])
                    interesting.AddRange(RegexUtils.InterestingStrings(output));
                // get the ones that are contiguous substrings
                var contiguous = interesting.Where(s => v.Contains((string)s))
                                            .Cast<string>();
                // get longest
                var longest = contiguous.Where(
                    s => !contiguous.Any(c => (c.Contains(s) && !s.Equals(c)))
                );
                // get strings that are not substrings
                var others = interesting.Where(s => !v.Contains((string)s));
                examples[input] = longest.Concat(others).ToList();
            }
            return DisjunctiveExamplesSpec.From(examples);
        }


        [WitnessFunction(nameof(Semantics.Concat), 1, DependsOnParameters = new[] { 0 })]
        internal DisjunctiveExamplesSpec WitnessStringTwo(GrammarRule rule,
                                                          DisjunctiveExamplesSpec spec,
                                                          ExampleSpec stringBinding)
        {
            const string toicache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(toicache));
            if (spec.ProvidedInputs.All(input => (!toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item1.IsNullOrEmpty()) &&
                                                  !toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item2.IsNullOrEmpty()))
            {
                var ex = new Dictionary<State, IEnumerable<object>>();
                foreach (State input in spec.ProvidedInputs)
                {
                    string v = (string)(string)input.Bindings.Select(p => p.Value)
                                                  .FirstOrDefault();
                    List<string> tempstrs = new List<string>();
                    foreach (string str in spec.DisjunctiveExamples[input])
                    {
                        tempstrs.Add(str);
                    }
                    tempstrs = GetValidSlices(v, tempstrs);
                    ex[input] = tempstrs;
                }
                spec = DisjunctiveExamplesSpec.From(ex);
            }

            var examples = new Dictionary<State, IEnumerable<object>>();
            foreach (State input in spec.ProvidedInputs)
            {
                // first string of concat
                var rest = new List<object>();
                // go over all of the possible left strings
                string one = (string)stringBinding.Examples[input];
                // go over all of the outputs
                foreach (string output in spec.DisjunctiveExamples[input])
                {
                    // test if the left part matches the first part
                    // of this output and add if doesn't exist yet
                    if (!one.IsNullOrEmpty() && output.StartsWith(one))
                    {
                        string s = output.Substring(one.Length, output.Length - one.Length);
                        if (!rest.Contains(s))
                            rest.Add(s);
                    }
                }
                //}
                examples[input] = rest;
            }
            return DisjunctiveExamplesSpec.From(examples);
        }

        // Left position of SubStr
        [WitnessFunction(nameof(Semantics.SubStr), 1)]
        internal DisjunctiveExamplesSpec WitnessLeftPosition(GrammarRule rule,
                                                             DisjunctiveExamplesSpec spec)
        {
            const string toicache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(toicache));
            if (spec.ProvidedInputs.All(input => (!toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item1.IsNullOrEmpty()) &&
                                                  !toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item2.IsNullOrEmpty()))
            {
                var ex = new Dictionary<State, IEnumerable<object>>();
                foreach (State input in spec.ProvidedInputs)
                {
                    string v = (string)(string)input.Bindings.Select(p => p.Value)
                                                  .FirstOrDefault();
                    List<string> tempstrs = new List<string>();
                    foreach (string str in spec.DisjunctiveExamples[input])
                    {
                        tempstrs.Add(str);
                    }
                    tempstrs = GetValidSlices(v, tempstrs);
                    ex[input] = tempstrs;
                }
                spec = DisjunctiveExamplesSpec.From(ex);
            }

            Dictionary<State, List<string>> disjunctiveexamples = new();
            foreach (var item in spec.DisjunctiveExamples)
            {
                string src = (string)item.Key[rule.Body[0]];
                //List<string> validSlices = GetValidSlices(src, item.Value.ToList() as List<string>);
            }


            var lExamples = new Dictionary<State, IEnumerable<object>>();
            foreach (State input in spec.ProvidedInputs)
            {
                // input string
                var v = (string)input[rule.Body[0]];
                // get all occurrences of all disjunctive
                // examples in the input string
                var occurrences = new List<object>();
                foreach (string output in spec.DisjunctiveExamples[input])
                {
                    if (String.IsNullOrEmpty(output)) continue;
                    foreach (int occurence in v.AllIndexesOf(output))
                        occurrences.Add(occurence);
                }

                lExamples[input] = occurrences.Distinct().ToList();
            }
            return DisjunctiveExamplesSpec.From(lExamples);
        }

        // Right position of SubStr
        [WitnessFunction(nameof(Semantics.SubStr), 2, DependsOnParameters = new[] { 1 })]
        internal DisjunctiveExamplesSpec WitnessRightPosition(GrammarRule rule,
                                                              DisjunctiveExamplesSpec spec,
                                                              ExampleSpec leftBinding)
        {
            const string toicache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(toicache));
            if (spec.ProvidedInputs.All(input => (!toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item1.IsNullOrEmpty()) &&
                                                  !toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item2.IsNullOrEmpty()))
            {
                var ex = new Dictionary<State, IEnumerable<object>>();
                foreach (State input in spec.ProvidedInputs)
                {
                    string v = (string)input.Bindings.Select(p => p.Value)
                                                  .FirstOrDefault();
                    List<string> tempstrs = new List<string>();
                    foreach (string str in spec.DisjunctiveExamples[input])
                    {
                        tempstrs.Add(str);
                    }
                    tempstrs = GetValidSlices(v, tempstrs);
                    ex[input] = tempstrs;
                }
                spec = DisjunctiveExamplesSpec.From(ex);
            }

            var rExamples = new Dictionary<State, IEnumerable<object>>();
            foreach (State input in spec.ProvidedInputs)
            {
                // get left index
                int left = (int)leftBinding.Examples[input];
                // get the string
                var v = (string)input[rule.Body[0]];
                // given the left and the string, compute the
                // right part
                var occurences = new List<object>();
                foreach (string output in spec.DisjunctiveExamples[input])
                {
                    // compute right position
                    int p = left + output.Length;
                    // check if within  bounds
                    if (left + output.Length <= v.Length)
                    {
                        // check if output is found in v at the left position
                        int match = v.IndexOf(output, left, output.Length);
                        if (match == left && !occurences.Contains(p))
                            occurences.Add(p);
                    }
                }
                rExamples[input] = occurences.Distinct().ToList();
            }
            return DisjunctiveExamplesSpec.From(rExamples);
        }

        // Constant for ConstStr is just the constant itself.
        [WitnessFunction(nameof(Semantics.ConstStr), 0)]
        internal DisjunctiveExamplesSpec WitnessConstant(GrammarRule rule,
                                                         DisjunctiveExamplesSpec spec)
        {
            const string toicache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(toicache));
            if (spec.ProvidedInputs.All(input => (!toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item1.IsNullOrEmpty()) &&
                                                  !toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item2.IsNullOrEmpty()))
            {
                var ex = new Dictionary<State, IEnumerable<object>>();
                foreach (State input in spec.ProvidedInputs)
                {
                    string v = (string)(string)input.Bindings.Select(p => p.Value)
                                                  .FirstOrDefault();
                    List<string> tempstrs = new List<string>();
                    foreach (string str in spec.DisjunctiveExamples[input])
                    {
                        tempstrs.Add(str);
                    }
                    tempstrs = GetValidSlices(v, tempstrs);
                    ex[input] = tempstrs;
                }
                spec = DisjunctiveExamplesSpec.From(ex);
            }

            // get all values
            var examples = new Dictionary<State, IEnumerable<object>>();
            foreach (State input in spec.ProvidedInputs)
                examples[input] = spec.DisjunctiveExamples[input];
            // itersect
            return DisjunctiveExamplesSpec.From(LearningUtils.Intersect(examples));
        }


        [WitnessFunction(nameof(Semantics.AbsPos), 1)]
        internal DisjunctiveExamplesSpec WitnessK(GrammarRule rule, DisjunctiveExamplesSpec spec)
        {
            // collect all positions
            var examples = new Dictionary<State, IEnumerable<object>>();
            foreach (State input in spec.ProvidedInputs)
            {
                var v = (string)input[rule.Body[0]];
                var p = new List<object>();
                foreach (int pos in spec.DisjunctiveExamples[input])
                {
                    p.Add(pos);
                    if (pos > 0 & pos <= v.Length)
                        p.Add(pos - v.Length - 1);
                }
                examples[input] = p;
            }
            return DisjunctiveExamplesSpec.From(LearningUtils.Intersect(examples));
        }

        // Left match
        [WitnessFunction(nameof(Semantics.RegPos), 1)]
        DisjunctiveExamplesSpec WitnessLeftRegex(GrammarRule rule, DisjunctiveExamplesSpec spec)
        {
            var result = new Dictionary<State, IEnumerable<object>>();
            foreach (var example in spec.DisjunctiveExamples)
            {
                State inputState = example.Key;
                var x = (string)inputState[rule.Body[0]];
                // Get all positions, are cached anyway.
                RegexUtils.BuildStringMatches(x,
                                              out List<Tuple<System.Text.RegularExpressions.Match, Regex>>[] leftMatches,
                                              out List<Tuple<System.Text.RegularExpressions.Match, Regex>>[] rightMatches);
                var regexes = new List<Regex>();
                foreach (int? pos in example.Value)
                    regexes.AddRange(leftMatches[pos.Value].Select(t => t.Item2));
                if (regexes.Count == 0)
                    return null;
                result[inputState] = regexes;
            }
            return new DisjunctiveExamplesSpec(LearningUtils.Intersect(result));
        }

        // Right match, independent of the other one
        [WitnessFunction(nameof(Semantics.RegPos), 2)]
        static DisjunctiveExamplesSpec WitnessRightRegex(GrammarRule rule, DisjunctiveExamplesSpec spec)
        {
            var result = new Dictionary<State, IEnumerable<object>>();
            foreach (var example in spec.DisjunctiveExamples)
            {
                State inputState = example.Key;
                var x = (string)inputState[rule.Body[0]];
                // Get all positions, are cached anyway.
                RegexUtils.BuildStringMatches(x,
                                              out List<Tuple<System.Text.RegularExpressions.Match, Regex>>[] leftMatches,
                                              out List<Tuple<System.Text.RegularExpressions.Match, Regex>>[] rightMatches);
                var regexes = new List<Regex>();
                foreach (int? pos in example.Value)
                    regexes.AddRange(rightMatches[pos.Value].Select(t => t.Item2));
                result[inputState] = regexes;
            }
            return new DisjunctiveExamplesSpec(LearningUtils.Intersect(result));
        }

        // Copied from PROSE website.
        [WitnessFunction(nameof(Semantics.RegPos), 3, DependsOnParameters = new[] { 1, 2 })]
        DisjunctiveExamplesSpec WitnessKForRegexPair(GrammarRule rule, DisjunctiveExamplesSpec spec,
                                                     ExampleSpec lSpec, ExampleSpec rSpec)
        {
            var result = new Dictionary<State, IEnumerable<object>>();
            foreach (var example in spec.DisjunctiveExamples)
            {
                State inputState = example.Key;
                var left = (Regex)lSpec.Examples[inputState];
                var right = (Regex)rSpec.Examples[inputState];
                if (left.ToString() == "" && right.ToString() == "")
                {
                    result[inputState] = new List<object>();
                }
                else
                {
                    var x = (string)inputState[rule.Body[0]];
                    var rightMatches = right.Matches(x).Cast<System.Text.RegularExpressions.Match>().ToDictionary(m => m.Index);
                    var matchPositions = new List<int>();
                    foreach (System.Text.RegularExpressions.Match m in left.Matches(x))
                    {
                        if (rightMatches.ContainsKey(m.Index + m.Length))
                            matchPositions.Add(m.Index + m.Length);
                    }
                    var ks = new HashSet<int?>();
                    foreach (int? pos in example.Value)
                    {
                        int occurrence = matchPositions.BinarySearch(pos.Value);
                        if (occurrence < 0) continue;
                        ks.Add(occurrence);
                        ks.Add(occurrence - matchPositions.Count);
                    }
                    if (ks.Count == 0)
                        return null;
                    result[inputState] = ks.Cast<object>().ToList();
                }
            }
            return new DisjunctiveExamplesSpec(LearningUtils.Intersect(result));
        }

        public static string[] DirGen = { "R", "L" };

        [WitnessFunction(nameof(Semantics.SemPos), 1, DependsOnParameters = new[] { 2 })]
        internal DisjunctiveExamplesSpec WitnessPosQuery(GrammarRule rule, DisjunctiveExamplesSpec spec,
                                                         ExampleSpec directionBinding)
        {

            const string TOICache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(TOICache));
            bool flag = true;
            // If direction not consistent, return nothing,
            // else get the direction.
            var directions = directionBinding.Examples.Values.Distinct();

            if (directions.Count() != 1)
            {
                // Queries for each state are the same.
                var eExamples = new Dictionary<State, IEnumerable<object>>();
                foreach (var example in spec.DisjunctiveExamples)
                    eExamples[example.Key] = new List<object>();
                return DisjunctiveExamplesSpec.From(eExamples);
            }

            string direction = (string)directions.First();


            // First, we gather all the sides.
            var sides = new Dictionary<string, List<string>>();
            List<string> srcs = new();
            foreach (var example in spec.DisjunctiveExamples)
            {
                State inputState = example.Key;
                string inputString = (string)inputState[rule.Body[0]];
                srcs.Add(inputString);

                if (spec.ProvidedInputs.All(input => (!toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item1.IsNullOrEmpty()) &&
                                                      !toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item2.IsNullOrEmpty()))
                {

                    if (direction == "L")
                    {
                        List<string> tempside = example.Value.Select(p => inputString.Substring(Convert.ToInt32(p))).ToList();
                        for (int i = 0; i < tempside.Count; i++)
                        {
                            string s = tempside[i];
                            Tuple<string, string> currtoi = toiDict[inputString];
                            if (inputString.Contains(currtoi.Item1) && !inputString.Contains(currtoi.Item2) && s.Equals(inputString.Substring(inputString.IndexOf(currtoi.Item1) + currtoi.Item1.Length)))
                            {
                                s = currtoi.Item1 + " " + s;
                                tempside[i] = s;
                            }
                        }
                        sides[inputString] = tempside;
                    }
                    if (direction == "R")
                    {
                        List<string> tempside = example.Value.Select(p => inputString.Substring(0, Convert.ToInt32(p))).ToList();
                        for (int i = 0; i < tempside.Count; i++)
                        {
                            string s = tempside[i];
                            Tuple<string, string> currtoi = toiDict[inputString];
                            if (inputString.Contains(currtoi.Item2) && s.Equals(inputString.Substring(0, inputString.IndexOf(currtoi.Item2) + currtoi.Item2.Length)))
                            {
                                s = s + " " + currtoi.Item2;
                                tempside[i] = s;
                            }
                        }
                        sides[inputString] = tempside;
                    }
                }
                else
                {
                    flag = false;
                    if (direction == "L") sides[inputString] = example.Value.Select(p => inputString.Substring(Convert.ToInt32(p))).ToList();
                    if (direction == "R") sides[inputString] = example.Value.Select(p => inputString.Substring(0, Convert.ToInt32(p))).ToList();
                }
            }

            var positions = new Dictionary<string, List<string>>();
            foreach (var side in sides)
            {
                Tuple<string, string> currtoi = toiDict[side.Key];
                positions[side.Key] = side.Value.Select(s => RegexUtils.DirectedInterestingStrings(s, direction)
                                                                       .OrderBy(x => x.Length))
                                                .SelectMany(x => x)
                                                .ToList();
            }

            // some position no
            if (positions.Values.Any(p => p.Count == 0))
                return null;

            // cluster copy
            List<List<string>> clusters;


            List<Tuple<string, List<string>>> temppositions = positions.Select(kvp => new Tuple<string, List<string>>(kvp.Key, kvp.Value.ToList())).ToList();

            if (Clustering)
                if (flag) clusters = ClusteringUtils.ClusterGreedy_TOI(temppositions);
                else clusters = ClusteringUtils.ClusterGreedy(positions.Values.Select(x => x.ToList()).ToList());
            else
                clusters = ClusteringUtils.ClusterAll(positions.Values.Select(x => x.ToList()).ToList());



            List<object> queries = new(clusters.Count);
            foreach (List<string> cluster in clusters)
                queries.Add(ClusteringUtils.BuildQuery(cluster, positions));

            // Queries for each state are the same.
            var kExamples = new Dictionary<State, IEnumerable<object>>();
            foreach (State state in spec.DisjunctiveExamples.Keys)
                kExamples[state] = queries;

            return DisjunctiveExamplesSpec.From(kExamples);
        }

        [WitnessFunction(nameof(Semantics.SemMap), 1)]
        internal DisjunctiveExamplesSpec WitnessMapQuery(GrammarRule rule,
                                                         DisjunctiveExamplesSpec spec)
        {

            Dictionary<string, List<string>> maps = spec.DisjunctiveExamples.ToDictionary(
                item => (string)item.Key[rule.Body[0]],
                item => item.Value.Cast<string>().ToList()
            );

            List<Tuple<string, string>[]> queries = new();
            List<List<string>> clusters;
            List<Tuple<string, List<string>>> tempmaps = maps.Select(kvp => new Tuple<string, List<string>>(kvp.Key, kvp.Value.ToList())).ToList();
            const string TOICache = @"../../../../FlashGPT3/cache/toi.json";
            Dictionary<string, Tuple<string, string>> toiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(TOICache));
            if (Clustering)
            {
                bool greedyortoi = spec.ProvidedInputs.All(input => (!toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item1.IsNullOrEmpty()) &&
                                                      !toiDict[(string)input.Bindings.Select(p => p.Value).FirstOrDefault()].Item2.IsNullOrEmpty());
                if (greedyortoi)
                    clusters = ClusteringUtils.ClusterGreedy_TOI(tempmaps);
                else
                    clusters = ClusteringUtils.ClusterGreedy(maps.Values.Select(x => x.ToList()).ToList());
            }
                
            else
                clusters = ClusteringUtils.ClusterAll(maps.Values.Select(x => x.ToList()).ToList());

            // convert back to queries
            foreach (List<string> cluster in clusters)
                queries.Add(ClusteringUtils.BuildQuery(cluster, maps));

            //Queries for each state are the same.
            var kExamples = new Dictionary<State, IEnumerable<object>>();
            foreach (State state in spec.DisjunctiveExamples.Keys)
                kExamples[state] = queries;

            // check where is different in queries
            DisjunctiveExamplesSpec res = DisjunctiveExamplesSpec.From(kExamples);
            return res;
        }

        private Dictionary<string, Tuple<string, string>> LoadCache(string file)
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
        private void SaveCache(string file, dynamic _cache)
        {
            if (!Directory.GetParent(file).Exists)
                Directory.GetParent(file).Create();
            File.WriteAllText(file, JsonConvert.SerializeObject(_cache,
                                                                Formatting.Indented));
        }

    }

}