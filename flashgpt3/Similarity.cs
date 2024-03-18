using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;

namespace FlashGPT3
{
    internal class Similarity
    {
        public static float GetCosineSimilarity(float[] src, float[] tgt)
        {
            if (src.Length != tgt.Length)
            {
                throw new ArgumentException("Vectors must have the same dimensionality");
            }

            float similarity = Distance.Cosine(src, tgt);

            return similarity;
        }
    }
}
