using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlashGPT3
{
    public class RESTJSONUtil
    {
        public static List<float[]> getFloarArray(string inputString)
        {

            string[] lines = inputString.Replace("\r", "").Split('\n');

            // Extract float values from each line
            List<float[]> resultList = new List<float[]>();
            List<float> currentList = new List<float>();

            foreach (var line in lines)
            {
                // Remove whitespace and brackets
                string cleanLine = line.Trim(' ', '[', ']', ',');

                // Extract only lines containing numeric values
                if (float.TryParse(cleanLine, out float value))
                {
                    currentList.Add(value);
                }
                else if (currentList.Count > 0)
                {
                    resultList.Add(currentList.ToArray());
                    currentList.Clear();
                }
            }

            // Add the last list if it's not empty
            if (currentList.Count > 0)
            {
                resultList.Add(currentList.ToArray());
            }

            return resultList;
        }
    }
}
