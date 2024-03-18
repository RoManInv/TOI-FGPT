using Microsoft.ProgramSynthesis.Transformation.Formula.Semantics.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FlashGPT3
{
    internal class StringUtils
    {
        public static readonly Regex alphanumeric = new Regex(@"[a-zA-Z0-9]*", RegexOptions.Compiled);

        class MyStrComparer : Comparer<string>
        {
            string delimiter;
            bool isAscending;

            public MyStrComparer(string aStr, bool ascending)
            {
                delimiter = aStr;
                isAscending = ascending;
            }

            public override int Compare(string x, string y)
            {
                var r = GetMySubstring(x).CompareTo(GetMySubstring(y));
                return isAscending ? r : -r;
            }

            string GetMySubstring(string str)
            {
                return str.IndexOf(delimiter) != -1 ? str.Substring(str.LastIndexOf(delimiter)) : string.Empty;
            }

        }

        public static string[] findCommonSubstring(string left, string right)
        {
            List<string> result = new List<string>();
            string[] rightArray = right.Split();
            string[] leftArray = left.Split();

            result.AddRange(leftArray.Where(l => rightArray.Any(r => r.StartsWith(l))));

            String[] resultTokArr = result.Distinct().ToArray();
            //Array.Sort(resultTokArr, resIndex.ToArray());
            List<string> resultArr = new List<string>();

            string currstr = "";
            foreach (string str in resultTokArr)
            {

                if (String.IsNullOrEmpty(currstr))
                {
                    currstr = str;
                }
                else
                {
                    currstr = currstr + " " + str;
                }
                if (!left.Contains(currstr) || !right.Contains(currstr))
                {
                    string tempstr = str;
                    currstr = string.Join(" ", currstr.Split(" ").Take(currstr.Split().Length - 1));
                    resultArr.Add(currstr);
                    currstr = tempstr;
                }
            }
            if (left.Contains(currstr) && right.Contains(currstr) && !resultArr.Contains(currstr))
            {
                if(left.StartsWith(currstr) && right.StartsWith(currstr))
                {
                    resultArr.Add(currstr);
                } else if (!left.StartsWith(currstr) && left[left.IndexOf(currstr) - 1].Equals(' ') && !right.StartsWith(currstr) && right[right.IndexOf(currstr) - 1].Equals(' '))
                {
                    resultArr.Add(currstr);
                } else if (!left.StartsWith(currstr) && left[left.IndexOf(currstr) - 1].Equals(' ') && right.StartsWith(currstr))
                {
                    resultArr.Add(currstr);
                } else if (left.StartsWith(currstr) && !right.StartsWith(currstr) && right[right.IndexOf(currstr) - 1].Equals(' '))
                {
                    resultArr.Add(currstr);
                }
                
            }

            return resultArr.ToArray();
        }

        public static List<string> GetAllSubstringForward(string str)
        {
            if (str.IsNullOrEmpty())
            {
                return new List<string>();
            }
            List<string> substrings = new List<string>();

            for (int i = 1; i <= str.Length; i++)
            {
                substrings.Add(str.Substring(0, i));
            }

            return substrings;
        }

        public static List<string> GetAllSubStringBackward(string str)
        {
            if (str.IsNullOrEmpty())
            {
                return new List<string>();
            }
            List<string> substrings = new List<string>();

            for (int i = str.Length; i > 0; i--)
            {
                substrings.Add(str.Substring(str.Length - i));
            }

            return substrings;
        }
    }
}
