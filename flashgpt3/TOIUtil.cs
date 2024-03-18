using AngleSharp.Common;
using Microsoft.ML.Transforms;
using Microsoft.ProgramSynthesis.Transformation.Formula.Semantics.Extensions;
using Microsoft.ProgramSynthesis.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FlashGPT3
{
    public class TOIIdentSim
    {
        private string[] stopwords = new[] { "a", "an", "at", "the", "and", "it", "for", "or", "of", "but", "in", "my",
            "your", "our", "their", "from", "than", "to", "towards", "but", "by", "very", "much", "is", "are",
            "be", "must", "should", "shall", "i", "me", "she", "her", "he", "him"};
        private float stopwordPunishment = 1.25f;
        internal static string delim_pattern = @"(\W+)";

        public static Boolean isAlphaNumeric(string strToCheck)
        {
            Regex rg = new Regex(@"^[^a-zA-Z0-9\s,.]*$");
            return !rg.IsMatch(strToCheck);
        }
        public static Boolean isNumeric(string strToCheck)
        {
            Regex rg = new Regex(@"^[0-9\s,\- ]*$");
            //return rg.IsMatch(strToCheck);
            foreach (string s in strToCheck.Split())
            {
                if (rg.IsMatch(s)) return true;
            }
            return false;
        }

        public static string RemoveNonAlphanumeric(string str)
        {
            Regex rgx = new Regex(@"[^a-zA-Z0-9\+\- ]");
            return rgx.Replace(str, "");
        }

        private float GetDistAvg(string str, Dictionary<string, float[]> observedTOI)
        {
            float dist = 0;
            EmbeddingHelper embHelper = new OpenAIEmbeddingHelper();
            float[] emb = embHelper.GetEmb(str);
            List<float> distlist = observedTOI.Values.Select(s => Similarity.GetCosineSimilarity(emb, s)).ToList();
            dist = distlist.Count > 0 ?
                   (float)(distlist.Average()) :
                   0.0f;
            return dist;
        }

        public (Tuple<string, string>, Dictionary<string, float[]>) GetTOI_naive(string left, string right, Dictionary<string, float[]> observedTOI)
        {
            TOIIdent toi = new TOIIdent();
            List<string> substrInMiddle = toi.ExtractTokInTheMiddle(left, right);
            EmbeddingHelper embHelper = new OpenAIEmbeddingHelper();
            Tuple<string, string> result = new Tuple<string, string>(substrInMiddle[0], substrInMiddle[1]);
            if (!observedTOI.Keys.Contains(result.Item1 + " " + result.Item2)) { observedTOI[result.Item1 + " " + result.Item2] = embHelper.GetEmb(result.Item1 + " " + result.Item2); }
            return (result, observedTOI);
        }

        public Dictionary<string, Tuple<string, string>> GetAllTOIs(Dictionary<string, string> examples)
        {
            Dictionary<string, Tuple<string, string>> tois = new Dictionary<string, Tuple<string, string>>();
            TOIIdent toiIdent = new TOIIdent();

            Dictionary<string, Tuple<string, string>> example_removeSynt = new();
            foreach (var kvp in examples) 
            { 
                List<string> currmiddle = toiIdent.ExtractTokInTheMiddle(kvp.Key, kvp.Value);
                Tuple<string, string> midtuple = new Tuple<string, string>(currmiddle[0], currmiddle[1]);
                example_removeSynt.Add(kvp.Key, midtuple);
            }

            Dictionary<string, List<Tuple<string, string>>> ExampleTOICandidates = new();
            Dictionary<string, float[]> observedTOI = new();

            (ExampleTOICandidates, observedTOI) = HorizontalComparison(example_removeSynt);

            tois = VerticalComparison(ExampleTOICandidates, observedTOI);

            return tois;
        }

        public (Dictionary<string, List<Tuple<string, string>>>, Dictionary<string, float[]>) HorizontalComparison(Dictionary<string, Tuple<string, string>> examples)
        {
            Dictionary<string, List<Tuple<string, string>>> resultSet = new();
            Dictionary<string, float[]> observedTOI = new();

            foreach (var kvp in examples)
            {
                List<Tuple<string, string>> tempcurrres = new List<Tuple<string, string>>();
                Dictionary<string, float[]> tempobserved = new Dictionary<string, float[]>();
                (tempcurrres, tempobserved) = Horizontal_One(kvp.Value.Item1, kvp.Value.Item2);
                resultSet[kvp.Key] = tempcurrres;
                foreach (var item in tempobserved)
                {
                    if (!observedTOI.ContainsKey(item.Key)) observedTOI.Add(item.Key, item.Value);
                }
            }
            return (resultSet, observedTOI);
        }

        public (List<Tuple<string, string>>, Dictionary<string, float[]>) Horizontal_One(string left, string right)
        {
            List<Tuple<string, string>> results = new();
            Dictionary<string, float[]> observedTOI = new();

            TOIIdent toi = new TOIIdent();
            EmbeddingHelper embHelper = new OpenAIEmbeddingHelper();
            Tuple<string, string> result;
            var leftEmb = embHelper.GetWordEmb(left);
            var rightEmb = embHelper.GetWordEmb(right);
            string maxLeft = left.Split()[0], maxRight = right.Split()[0];
            string currLeft, currRight;
            float[] currLeftEmb, currRightEmb;
            float maxDist = 2.0f;
            Tuple<string, string> emptyTOI = new Tuple<string, string>("", "");
            List<Tuple<string, string>> emptyResult = new List<Tuple<string, string>>();
            emptyResult.Add(emptyTOI);

            List<string> substrInMiddle = toi.ExtractTokInTheMiddle(left, right);
            string lsub = substrInMiddle[0];
            string rsub = substrInMiddle[1];
            if (!isAlphaNumeric(lsub) || !isAlphaNumeric(rsub)) return (emptyResult, observedTOI);
            if (lsub.IsNullOrEmpty() || rsub.IsNullOrEmpty()) return (emptyResult, observedTOI);
            if (isNumeric(RemoveNonAlphanumeric(lsub)) || isNumeric(RemoveNonAlphanumeric(rsub))) return (emptyResult, observedTOI);
            if (RemoveNonAlphanumeric(lsub).Equals(rsub) || RemoveNonAlphanumeric(rsub).Equals(lsub)) return (emptyResult, observedTOI);

            Dictionary<Tuple<string, string>, float> AllCandidates = new Dictionary<Tuple<string, string>, float>();

            AllCandidates[new Tuple<string, string>(lsub, rsub)] = GetDistAvg(lsub + " " + rsub, observedTOI);
            foreach (string i in lsub.Split())
            {
                if (i is null || string.IsNullOrEmpty(i))
                {
                    continue;
                }
                foreach (string j in rsub.Split())
                {
                    if (j is null || string.IsNullOrEmpty(j) || Regex.Replace(i, @"[^\w]", string.Empty).Equals(Regex.Replace(j, @"[^\w]", string.Empty))) { continue; }
                    //Console.WriteLine("Sim for " + i + " and " + j);
                    float[] lemb; float[] remb;
                    if (leftEmb.Keys.Contains(i)) lemb = leftEmb[i];
                    else lemb = embHelper.GetEmb(i);
                    if (rightEmb.Keys.Contains(j)) remb = rightEmb[j];
                    else remb = embHelper.GetEmb(j);

                    float dist = Similarity.GetCosineSimilarity(lemb, remb);
                    if (stopwords.Contains(i.ToLower()) || stopwords.Contains(j.ToLower()))
                    {
                        dist = dist * stopwordPunishment;
                    }
                    if (dist > 10e-6 && dist < maxDist)
                    {
                        maxDist = dist;
                        maxLeft = i;
                        maxRight = j;
                    }
                }
            }

            currLeft = maxLeft;
            currRight = maxRight;
            currLeftEmb = embHelper.GetEmb(currLeft);
            currRightEmb = embHelper.GetEmb(currRight);
            float[] currLeftRightEmb = embHelper.GetEmb(currLeft + " " + currRight);
            if (observedTOI.Count > 0)
            {
                AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
            }
            else
            {
                AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
            }

            bool continueFlag = true;
            bool leftGreedLeft = false;
            bool leftGreedRight = false;
            bool rightGreedLeft = false;
            bool rightGreedRight = false;
            while (continueFlag)
            {
                if (!leftGreedLeft)
                {
                    if (left.StartsWith(currLeft))
                    {
                        leftGreedLeft = true;
                        continue;
                    }
                    int? idx = left.Split().IndexOf(currLeft.Split()[0]);
                    int idxnotnull;
                    if (idx is not null && idx > 0)
                    {
                        idxnotnull = (int)(idx - 1);
                    }
                    else
                    {
                        idxnotnull = 0;
                    }
                    if (idxnotnull <= 0) { leftGreedLeft = true; continue; }
                    var tempLeft = left.Split()[idxnotnull] + " " + currLeft;
                    var tempLeftEmb = embHelper.GetEmb(tempLeft);
                    var tempDist = Similarity.GetCosineSimilarity(tempLeftEmb, currRightEmb);
                    if (stopwords.Contains(left.Split()[idxnotnull].ToLower()))
                    {
                        tempDist = tempDist * stopwordPunishment;
                    }
                    if (tempDist < maxDist)
                    {
                        maxDist = tempDist;
                        currLeftEmb = tempLeftEmb;
                        currLeft = tempLeft;

                        if (observedTOI.Count > 0)
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
                        }
                        else
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
                        }
                    }
                    else
                    {
                        leftGreedLeft = true;
                    }
                }
                if (!leftGreedRight)
                {
                    if (left.EndsWith(currLeft))
                    {
                        leftGreedRight = true;
                        continue;
                    }

                    int? idx = left.Split().IndexOf(currLeft.Split()[^1]);
                    int idxnotnull;
                    if (idx is not null)
                    {
                        idxnotnull = (int)(idx + 1);
                    }
                    else
                    {
                        idxnotnull = 0;
                    }
                    if (idxnotnull > left.Split().Length) { leftGreedRight = true; continue; }
                    var tempLeft = currLeft + " " + left.Split()[idxnotnull];
                    var tempLeftEmb = embHelper.GetEmb(tempLeft);
                    var tempDist = Similarity.GetCosineSimilarity(tempLeftEmb, currRightEmb);
                    if (stopwords.Contains(left.Split()[idxnotnull].ToLower()))
                    {
                        tempDist = tempDist * stopwordPunishment;
                    }
                    if (tempDist < maxDist)
                    {
                        maxDist = tempDist;
                        currLeftEmb = tempLeftEmb;
                        currLeft = tempLeft;
                        if (observedTOI.Count > 0)
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
                        }
                        else
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
                        }
                    }
                    else
                    {
                        leftGreedRight = true;
                    }
                }
                if (!rightGreedLeft)
                {
                    if (right.StartsWith(currRight))
                    {
                        rightGreedLeft = true;
                        continue;
                    }

                    int? idx = right.Split().IndexOf(currRight.Split()[0]);
                    int idxnotnull;
                    if (idx is not null)
                    {
                        idxnotnull = (int)(idx - 1);
                    }
                    else
                    {
                        idxnotnull = 0;
                    }
                    if (idxnotnull <= 0) { rightGreedLeft = true; continue; }
                    var tempRight = right.Split()[idxnotnull] + " " + currRight;
                    var tempRighrEmb = embHelper.GetEmb(tempRight);
                    var tempDist = Similarity.GetCosineSimilarity(tempRighrEmb, currLeftEmb);
                    if (stopwords.Contains(right.Split()[idxnotnull].ToLower()))
                    {
                        tempDist = tempDist * stopwordPunishment;
                    }
                    if (tempDist < maxDist)
                    {
                        maxDist = tempDist;
                        currRightEmb = tempRighrEmb;
                        currRight = tempRight;
                        if (observedTOI.Count > 0)
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
                        }
                        else
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
                        }
                    }
                    else
                    {
                        rightGreedLeft = true;
                    }
                }
                if (!rightGreedRight)
                {
                    if (right.EndsWith(currRight))
                    {
                        rightGreedRight = true;
                        continue;
                    }

                    int? idx = right.Split().IndexOf(currRight.Split()[^1]);
                    int idxnotnull;
                    if (idx is not null)
                    {
                        idxnotnull = (int)(idx + 1);
                    }
                    else
                    {
                        idxnotnull = 0;
                    }
                    if (idxnotnull > right.Split().Length) { rightGreedRight = true; continue; }
                    var tempRight = currRight + " " + right.Split()[idxnotnull];
                    var tempRighrEmb = embHelper.GetEmb(tempRight);
                    var tempDist = Similarity.GetCosineSimilarity(tempRighrEmb, currLeftEmb);
                    if (stopwords.Contains(right.Split()[idxnotnull].ToLower()))
                    {
                        tempDist = tempDist * stopwordPunishment;
                    }
                    if (tempDist < maxDist)
                    {
                        maxDist = tempDist;
                        currRightEmb = tempRighrEmb;
                        currRight = tempRight;
                        if (observedTOI.Count > 0)
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
                        }
                        else
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
                        }
                    }
                    else
                    {
                        rightGreedRight = true;
                    }
                }
                if (leftGreedLeft && leftGreedRight && rightGreedLeft && rightGreedRight)
                {
                    continueFlag = false;
                }
            }

            int toiCount = AllCandidates.Count >= 3 ? 3 : AllCandidates.Count;

            List<Tuple<string, string>> maxKeyValue = AllCandidates.OrderByDescending(kv => kv.Value)
                                                                   .ThenByDescending(kv => kv.Key.Item1.Length + kv.Key.Item2.Length)
                                                                   .Take(toiCount)
                                                                   .ToDictionary<Tuple<string, string>, float>()
                                                                   .Keys.ToList();

            foreach (var item in maxKeyValue)
            {
                observedTOI[item.Item1 + " " + item.Item2] = embHelper.GetEmb(item.Item1 + " " + item.Item2);
            }

            return (maxKeyValue, observedTOI);
        }

        public Dictionary<string, Tuple<string, string>> VerticalComparison(Dictionary<string, List<Tuple<string, string>>> ExampleTOICandidates, Dictionary<string, float[]> observedTOI)
        {
            Dictionary<string, Tuple<string, string>> result = new();
            EmbeddingHelper embHelper = new OpenAIEmbeddingHelper();

            if (ExampleTOICandidates.All(kvp => kvp.Value.All(t => t.Item1.IsNullOrEmpty() && t.Item2.IsNullOrEmpty())))
            {
                foreach (var kvp in ExampleTOICandidates)
                {
                    result.Add(kvp.Key, new Tuple<string, string>("", ""));
                }
                return result;
            }

            if (ExampleTOICandidates.Keys.Count == 1)
            {
                result.Add(ExampleTOICandidates.Keys.FirstOrDefault(), ExampleTOICandidates.Values
                                                                                           .OrderByDescending(t => t.Sum(ti => ti.Item1.Length + ti.Item2.Length))
                                                                                           .FirstOrDefault()
                                                                                           .FirstOrDefault());
                return result;
            }

            List<List<Tuple<string, string>>> cartesianProduct = GetCartesianProduct(ExampleTOICandidates);
            List<List<Tuple<string, string>[]>> combinations = new List<List<Tuple<string, string>[]>>();
            List<float> SumSimList = new List<float>();

            foreach (var item in cartesianProduct)
            {
                combinations.Add(GetCombinations(item));
            }

            foreach (var combination in combinations)
            {
                float sumSim = 0.0f;
                foreach (var c in combination)
                {
                    if (c[0].Item1.IsNullOrEmpty() && c[0].Item2.IsNullOrEmpty() && 
                        c[1].Item1.IsNullOrEmpty() && c[1].Item2.IsNullOrEmpty()) 
                        sumSim = 0;
                    else
                    {
                        float[] emb1;
                        float[] emb2;
                        if (observedTOI.Keys.Contains(c[0].Item1 + " " + c[0].Item2)) emb1 = observedTOI[c[0].Item1 + " " + c[0].Item2];
                        else
                        {
                            emb1 = embHelper.GetEmb(c[0].Item1 + " " + c[0].Item2);
                            observedTOI.Add(c[0].Item1 + " " + c[0].Item2, emb1);
                        }
                        if (observedTOI.Keys.Contains(c[1].Item1 + " " + c[1].Item2)) emb2 = observedTOI[c[1].Item1 + " " + c[1].Item2];
                        else
                        {
                            emb2 = embHelper.GetEmb(c[1].Item1 + " " + c[1].Item2);
                            observedTOI.Add(c[1].Item1 + " " + c[1].Item2, emb2);
                        }
                        sumSim += Similarity.GetCosineSimilarity(emb1, emb2);
                    }
                }
                SumSimList.Add(sumSim);
            }

            var sortedCombinations = cartesianProduct.Zip(SumSimList, (comb, sumSim) => new { Combinations = comb, SumSim = sumSim })
                                            .OrderByDescending(item => item.SumSim)
                                            .Select(item => item.Combinations)
                                            .ToList();

            List<Tuple<string, string>> highestCombination = sortedCombinations.FirstOrDefault();

            for (int i = 0; i < ExampleTOICandidates.Keys.Count; i++)
            {
                result[ExampleTOICandidates.Keys.GetItemByIndex(i)] = highestCombination[i];
            }

            return result;
        }

        public (Tuple<string, string>, Dictionary<string, float[]>) GetTOI(string left, string right, Dictionary<string, float[]> observedTOI)
        {
            TOIIdent toi = new TOIIdent();
            EmbeddingHelper embHelper = new OpenAIEmbeddingHelper();
            Tuple<string, string> result;
            var leftEmb = embHelper.GetWordEmb(left);
            var rightEmb = embHelper.GetWordEmb(right);
            string maxLeft = left.Split()[0], maxRight = right.Split()[0];
            string currLeft, currRight;
            float[] currLeftEmb, currRightEmb;
            float maxDist = 2.0f;

            List<string> substrInMiddle = toi.ExtractTokInTheMiddle(left, right);
            string lsub = substrInMiddle[0];
            string rsub = substrInMiddle[1];
            if (!isAlphaNumeric(lsub) || !isAlphaNumeric(rsub)) return (new Tuple<string, string>("", "") , observedTOI);
            if (lsub.IsNullOrEmpty() || rsub.IsNullOrEmpty()) return (new Tuple<string, string>("", ""), observedTOI);
            if (isNumeric(RemoveNonAlphanumeric(lsub)) || isNumeric(RemoveNonAlphanumeric(rsub))) return (new Tuple<string, string>("", ""), observedTOI);
            if (RemoveNonAlphanumeric(lsub).Equals(rsub) || RemoveNonAlphanumeric(rsub).Equals(lsub)) return (new Tuple<string, string>("", ""), observedTOI);

            Dictionary<Tuple<string, string>, float> AllCandidates = new Dictionary<Tuple<string, string>, float>();

            AllCandidates[new Tuple<string, string>(lsub, rsub)] = GetDistAvg(lsub + " " + rsub, observedTOI);
            foreach (string i in lsub.Split())
            {
                if (i is null || string.IsNullOrEmpty(i))
                {
                    continue;
                }
                foreach (string j in rsub.Split())
                {
                    if (j is null || string.IsNullOrEmpty(j) || Regex.Replace(i, @"[^\w]", string.Empty).Equals(Regex.Replace(j, @"[^\w]", string.Empty))) { continue; }
                    //Console.WriteLine("Sim for " + i + " and " + j);
                    float[] lemb; float[] remb;
                    if (leftEmb.Keys.Contains(i)) lemb = leftEmb[i];
                    else lemb = embHelper.GetEmb(i);
                    if (rightEmb.Keys.Contains(j)) remb = rightEmb[j];
                    else remb = embHelper.GetEmb(j);
                    
                    float dist = Similarity.GetCosineSimilarity(lemb, remb);
                    if (stopwords.Contains(i.ToLower()) || stopwords.Contains(j.ToLower()))
                    {
                        dist = dist * stopwordPunishment;
                    }
                    if (dist > 10e-6 && dist < maxDist)
                    {
                        maxDist = dist;
                        maxLeft = i;
                        maxRight = j;
                    }
                }
            }

            currLeft = maxLeft;
            currRight = maxRight;
            currLeftEmb = embHelper.GetEmb(currLeft);
            currRightEmb = embHelper.GetEmb(currRight);
            float[] currLeftRightEmb = embHelper.GetEmb(currLeft + " " + currRight);
            if (observedTOI.Count > 0)
            {
                AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
            }
            else
            {
                AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
            }

            bool continueFlag = true;
            bool leftGreedLeft = false;
            bool leftGreedRight = false;
            bool rightGreedLeft = false;
            bool rightGreedRight = false;
            while (continueFlag)
            {
                if (!leftGreedLeft)
                {
                    if (left.StartsWith(currLeft))
                    {
                        leftGreedLeft = true;
                        continue;
                    }
                    int? idx = left.Split().IndexOf(currLeft.Split()[0]);
                    int idxnotnull;
                    if (idx is not null && idx > 0)
                    {
                        idxnotnull = (int)(idx - 1);
                    }
                    else
                    {
                        idxnotnull = 0;
                    }
                    if (idxnotnull <= 0) { leftGreedLeft = true; continue; }
                    var tempLeft = left.Split()[idxnotnull] + " " + currLeft;
                    var tempLeftEmb = embHelper.GetEmb(tempLeft);
                    var tempDist = Similarity.GetCosineSimilarity(tempLeftEmb, currRightEmb);
                    if (stopwords.Contains(left.Split()[idxnotnull].ToLower()))
                    {
                        tempDist = tempDist * stopwordPunishment;
                    }
                    if (tempDist < maxDist)
                    {
                        maxDist = tempDist;
                        currLeftEmb = tempLeftEmb;
                        currLeft = tempLeft;

                        if (observedTOI.Count > 0)
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
                        }
                        else
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
                        }
                    }
                    else
                    {
                        leftGreedLeft = true;
                    }
                }
                if (!leftGreedRight)
                {
                    if (left.EndsWith(currLeft))
                    {
                        leftGreedRight = true;
                        continue;
                    }

                    int? idx = left.Split().IndexOf(currLeft.Split()[^1]);
                    int idxnotnull;
                    if (idx is not null)
                    {
                        idxnotnull = (int)(idx + 1);
                    }
                    else
                    {
                        idxnotnull = 0;
                    }
                    if (idxnotnull > left.Split().Length) { leftGreedRight = true; continue; }
                    var tempLeft = currLeft + " " + left.Split()[idxnotnull];
                    var tempLeftEmb = embHelper.GetEmb(tempLeft);
                    var tempDist = Similarity.GetCosineSimilarity(tempLeftEmb, currRightEmb);
                    if (stopwords.Contains(left.Split()[idxnotnull].ToLower()))
                    {
                        tempDist = tempDist * stopwordPunishment;
                    }
                    if (tempDist < maxDist)
                    {
                        maxDist = tempDist;
                        currLeftEmb = tempLeftEmb;
                        currLeft = tempLeft;
                        if (observedTOI.Count > 0)
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
                        }
                        else
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
                        }
                    }
                    else
                    {
                        leftGreedRight = true;
                    }
                }
                if (!rightGreedLeft)
                {
                    if (right.StartsWith(currRight))
                    {
                        rightGreedLeft = true;
                        continue;
                    }

                    int? idx = right.Split().IndexOf(currRight.Split()[0]);
                    int idxnotnull;
                    if (idx is not null)
                    {
                        idxnotnull = (int)(idx - 1);
                    }
                    else
                    {
                        idxnotnull = 0;
                    }
                    if (idxnotnull <= 0) { rightGreedLeft = true; continue; }
                    var tempRight = right.Split()[idxnotnull] + " " + currRight;
                    var tempRighrEmb = embHelper.GetEmb(tempRight);
                    var tempDist = Similarity.GetCosineSimilarity(tempRighrEmb, currLeftEmb);
                    if (stopwords.Contains(right.Split()[idxnotnull].ToLower()))
                    {
                        tempDist = tempDist * stopwordPunishment;
                    }
                    if (tempDist < maxDist)
                    {
                        maxDist = tempDist;
                        currRightEmb = tempRighrEmb;
                        currRight = tempRight;
                        if (observedTOI.Count > 0)
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
                        }
                        else
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
                        }
                    }
                    else
                    {
                        rightGreedLeft = true;
                    }
                }
                if (!rightGreedRight)
                {
                    if (right.EndsWith(currRight))
                    {
                        rightGreedRight = true;
                        continue;
                    }

                    int? idx = right.Split().IndexOf(currRight.Split()[^1]);
                    int idxnotnull;
                    if (idx is not null)
                    {
                        idxnotnull = (int)(idx + 1);
                    }
                    else
                    {
                        idxnotnull = 0;
                    }
                    if (idxnotnull > right.Split().Length) { rightGreedRight = true; continue; }
                    var tempRight = currRight + " " + right.Split()[idxnotnull];
                    var tempRighrEmb = embHelper.GetEmb(tempRight);
                    var tempDist = Similarity.GetCosineSimilarity(tempRighrEmb, currLeftEmb);
                    if (stopwords.Contains(right.Split()[idxnotnull].ToLower()))
                    {
                        tempDist = tempDist * stopwordPunishment;
                    }
                    if (tempDist < maxDist)
                    {
                        maxDist = tempDist;
                        currRightEmb = tempRighrEmb;
                        currRight = tempRight;
                        if (observedTOI.Count > 0)
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = GetDistAvg(currLeft + " " + currRight, observedTOI);
                        }
                        else
                        {
                            AllCandidates[new Tuple<string, string>(currLeft, currRight)] = 1;
                        }
                    }
                    else
                    {
                        rightGreedRight = true;
                    }
                }
                if (leftGreedLeft && leftGreedRight && rightGreedLeft && rightGreedRight)
                {
                    continueFlag = false;
                }
            }

            Tuple<string, string> maxKeyValue = AllCandidates.OrderByDescending(kv => kv.Value)
                                                .ThenByDescending(kv => kv.Key.Item1.Length + kv.Key.Item2.Length)
                                                .FirstOrDefault()
                                                .Key;
            float maxVerticalDist = AllCandidates.OrderByDescending(kv => kv.Value)
                        .ThenByDescending(kv => kv.Key.Item1.Length + kv.Key.Item2.Length)
                        .FirstOrDefault()
                        .Value;

            if (maxVerticalDist < maxDist)
                result = maxKeyValue;
            else
                result = new Tuple<string, string>(currLeft, currRight);
            observedTOI[result.Item1 + " " + result.Item2] = embHelper.GetEmb(result.Item1 + " " + result.Item2);

            return (result, observedTOI);
        }

        static List<List<Tuple<string, string>>> GetCartesianProduct(Dictionary<string, List<Tuple<string, string>>> example)
        {
            // Get all the lists from the dictionary values
            IEnumerable<IEnumerable<Tuple<string, string>>> lists = example.Values;

            // Calculate the Cartesian product of all the lists
            IEnumerable<IEnumerable<Tuple<string, string>>> cartesianProduct = CartesianProduct(lists);

            // Convert the Cartesian product into a list of lists
            List<List<Tuple<string, string>>> result = cartesianProduct.Select(combination => combination.ToList()).ToList();

            return result;
        }

        internal static IEnumerable<IEnumerable<T>> CartesianProduct<T>(IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    from accseq in accumulator
                    from item in sequence
                    select accseq.Concat(new[] { item }));
        }

        static List<Tuple<string, string>[]> GetCombinations(List<Tuple<string, string>> strings)
        {
            // If the list has only one element, return that tuple without combining anything
            if (strings.Count == 1)
            {
                return new List<Tuple<string, string>[]> { new[] { strings[0] } };
            }

            // Get all combinations of tuple pairs
            List<Tuple<string, string>[]> combinations = strings.SelectMany((x, i) => strings.Skip(i + 1).Select(y => new[] { x, y })).ToList();

            return combinations;
        }

    }

    internal abstract class EmbeddingHelper
    {

        internal static string delim_pattern = @"(\W+)";
        public abstract Dictionary<string, float[]> GetWordEmb(string str);
        public abstract float[] GetEmb(string str);
    }

    internal class OpenAIEmbeddingHelper : EmbeddingHelper
    {
        public override Dictionary<string, float[]> GetWordEmb(string str)
        {
            Dictionary<string, float[]> wordemb = new Dictionary<string, float[]>();
            // str.Split(new[] { ' ' })
            foreach (string s in Regex.Split(str, delim_pattern))
            {
                var emb = EmbeddingQueryRunner.Run(s);
                float[] embArray = Array.ConvertAll(JsonConvert.SerializeObject(emb[0].Embedding).Split(new char[] { '[', ']', ',' }, StringSplitOptions.RemoveEmptyEntries), float.Parse);
                wordemb[s] = embArray;
            }


            return wordemb;
        }

        public override float[] GetEmb(string str)
        {
            var emb = EmbeddingQueryRunner.Run(str);
            float[] embArray = Array.ConvertAll(JsonConvert.SerializeObject(emb[0].Embedding).Split(new char[] { '[', ']', ',' }, StringSplitOptions.RemoveEmptyEntries), float.Parse);

            return embArray;
        }
    }

    internal class BERTEmbeddingHelper : EmbeddingHelper
    {
        public override Dictionary<string, float[]> GetWordEmb(string str)
        {
            Dictionary<string, float[]> wordemb = new Dictionary<string, float[]>();
            string reqURL = "http://127.0.0.1:5000/getembeddings?data=";
            byte[] bytesToEncode = System.Text.Encoding.UTF8.GetBytes(str);
            string base64EncodedString = Convert.ToBase64String(bytesToEncode);
            reqURL = reqURL + base64EncodedString;
            HttpClient httpClient = new HttpClient();
            var response = httpClient.GetAsync(reqURL).Result;
            string jsonString = response.Content.ReadAsStringAsync().Result;

            Dictionary<string, dynamic> res = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonString);
            List<string> resTokens = res["tokens"].ToObject<List<string>>();
            string resEmbeddings = res["embeddings"].ToString();

            List<float[]> resultList = RESTJSONUtil.getFloarArray(resEmbeddings);

            for (int i = 0; i < resultList.Count; i++)
            {
                wordemb[resTokens[i]] = resultList[i];
            }

            return wordemb;
        }

        public override float[] GetEmb(string str)
        {
            Dictionary<string, float[]> wordemb = new Dictionary<string, float[]>();
            string reqURL = "http://127.0.0.1:5000/getsentenceembeddings?data=";
            byte[] bytesToEncode = System.Text.Encoding.UTF8.GetBytes(str);
            string base64EncodedString = Convert.ToBase64String(bytesToEncode);
            reqURL = reqURL + base64EncodedString;
            HttpClient httpClient = new HttpClient();
            var response = httpClient.GetAsync(reqURL).Result;
            string jsonString = response.Content.ReadAsStringAsync().Result;

            Dictionary<string, dynamic> res = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonString);
            string resEmbeddings = res["embeddings"].ToString();

            List<float[]> resultList = RESTJSONUtil.getFloarArray(resEmbeddings);

            return resultList[0];
        }
    }
}
