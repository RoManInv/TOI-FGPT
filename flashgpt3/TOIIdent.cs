using Microsoft.ProgramSynthesis;
using Microsoft.ProgramSynthesis.Transformation.Formula.Semantics.Extensions;
using Microsoft.ProgramSynthesis.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FlashGPT3
{
    public class TOIIdent
    {

        // list of non-alphanumeric tokens from FlashGPT3
        internal char[] delimiters = new List<char>() { '\t', ',', '.', ':', ';', '!', '"', '\'', '/', '\\', 
                                                                '-', '*', '+', '_', '=', '>', '<', ']', '[', '}', '{', '|', 
                                                                '&', '#', '$', '^', '@', '%', '?', '~', '`', '\u2192', '\u2190' }.ToArray();
        //internal static string delim_pattern = @"(,.:;!\)\(""'/\\-\*\+_=><\]\[}{\|&#\$\^@%\?~`\\u2192\\u2190\p{Zs}+)";
        internal static string delim_pattern = @"(\W+)";
        internal Regex delim_regex = new Regex(delim_pattern, RegexOptions.Compiled);


        public List<string> ExtractTokInTheMiddle(string left, string right)
        {
            List<string> res = new List<string>();

            string[] strs = StringUtils.findCommonSubstring(left, right);
            if (strs.IsNullOrEmpty() || strs.All(s => s.IsNullOrEmpty()))
            {
                res.Add(left);
                res.Add(right);
                return res;
            }

            if (strs.Length == 1 && right.Contains(strs[0]) && right.IndexOf(strs[0]) > 0)
            {
                //int leftmostIdx = left.IndexOf(strs[0].Split().Take(1).ToArray()[0]);
                //int leftmostIdx = left.IndexOf(Regex.Split(strs[0], delim_pattern).Take(1).ToArray()[0]);
                string tempstr;
                // if it is the only string on the left
                if (left.Split().Length == 1) res.Add(left);
                else
                {
                    tempstr = left.Substring(0, left.IndexOf(strs[0])).Trim();
                    res.Add(tempstr);
                }
                

                //leftmostIdx = right.IndexOf(strs[0]);
                tempstr = right.Substring(0, right.IndexOf(strs[0])).Trim();
                res.Add(tempstr);
            }
            // strs[0].Split(delimiters)
            else if (strs.Length == 1 && right.Contains(strs[0]) && right.IndexOf(Regex.Split(strs[0], delim_pattern).Take(1).ToArray()[0]) == 0)
            {
                int rightmostindex = left.IndexOf(Regex.Split(strs[0], delim_pattern)[^1]);
                string tempstr = left.Substring(rightmostindex, Regex.Split(strs[0], delim_pattern)[^1].Length).Trim();
                res.Add(tempstr);

                rightmostindex = right.IndexOf(Regex.Split(strs[0], delim_pattern)[^1]);
                string currtempstr = right.Substring(rightmostindex).Trim();
                res.Add(currtempstr);
            }
            else
            {

                for (int i = 0; i < strs.Length - 1; i++)
                {
                    //strs[i].Length - strs[i].Split(delimiters)[^1].Length
                    
                    var leftmost = left.IndexOf(strs[i]) + strs[i].Length - Regex.Split(strs[i], delim_pattern)[^1].Length;
                    //var rightmost = left.IndexOf(strs[i + 1].Split()[0], leftmost + strs[i].Split()[^1].Length) + strs[i + 1].Split()[0].Length;
                    var rightmost = left.IndexOf(strs[i + 1], leftmost) + Regex.Split(strs[i + 1], delim_pattern)[0].Length;
                    //Console.WriteLine(left + "///" + right + "///" + strs[i]  + "///" + strs[i + 1]);
                    var tempstrLeft = left.Substring(leftmost, rightmost - leftmost).Trim();
                    res.Add(tempstrLeft);

                    //leftmost = right.IndexOf(strs[i].Split()[^1]);
                    leftmost = right.IndexOf(strs[i]) + strs[i].Length - Regex.Split(strs[i], delim_pattern)[^1].Length;
                    //rightmost = right.IndexOf(strs[i + 1].Split()[0], leftmost + strs[i].Split()[^1].Length) + strs[i + 1].Split()[0].Length;
                    rightmost = right.IndexOf(strs[i + 1], leftmost) + Regex.Split(strs[i + 1], delim_pattern)[0].Length;
                    var tempstrRight = right.Substring(leftmost, rightmost - leftmost).Trim();
                    res.Add(tempstrRight);
                }
            }



            return res;
        }

        public List<string> VerticalExtraction(List<string> lefts, List<string> rights)
        {
            List<string> res = new List<string>();
            List<string> extractions = new List<string>();

            foreach (var lr in lefts.Zip(rights, (l, r) => new { left = l, right = r }))
            {
                List<string> ext = ExtractTokInTheMiddle(lr.left, lr.right);
                extractions.Add(ext[1]);
            }

            Dictionary<string, int> leftmost = new Dictionary<string, int>();
            Dictionary<string, int> rightmost = new Dictionary<string, int>();

            foreach (string ext in extractions)
            {
                //ext.Split(delimiters)
                
                string leftmosttok = Regex.Split(ext, delim_pattern)[0];
                string rightmosttok = Regex.Split(ext, delim_pattern)[^1];
                if (leftmost.ContainsKey(leftmosttok))
                {
                    leftmost[leftmosttok] += 1;
                }
                else
                {
                    leftmost[leftmosttok] = 1;
                }
                if (rightmost.ContainsKey(rightmosttok))
                {
                    rightmost[rightmosttok] += 1;
                }
                else
                {
                    rightmost[rightmosttok] = 1;
                }
            }

            var leftmax = leftmost.Values.Max();
            var rightmax = rightmost.Values.Max();
            string[] leftremoves = leftmost.Where(x => x.Value == leftmax && x.Value != 1).Select(x => x.Key).ToArray();
            string[] rightremoves = rightmost.Where(x => x.Value == rightmax && x.Value != 1).Select(x => x.Key).ToArray();

            foreach (var ext in extractions)
            {
                string temp = ext;
                foreach (string lrm in leftremoves)
                {
                    if (temp.Contains(lrm) && temp.IndexOf(lrm) == 0)
                    {
                        
                        //temp.Split(' ')
                        temp = string.Join("", Regex.Split(temp, delim_pattern).Skip(1));
                    }
                }
                foreach (string rrm in rightremoves)
                {
                    if (temp.Contains(rrm) && temp.IndexOf(rrm) + rrm.Length == temp.Length)
                    {
                        var cnt = Regex.Split(temp, delim_pattern).Count();
                        temp = string.Join("", Regex.Split(temp, delim_pattern).Take(cnt - 1));
                    }
                }

                res.Add(temp);

            }

            return res;
        }
    }
}
