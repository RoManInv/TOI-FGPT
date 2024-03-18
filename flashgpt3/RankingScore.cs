using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.ProgramSynthesis;
using Microsoft.ProgramSynthesis.AST;
using Microsoft.ProgramSynthesis.DslLibrary;
using Microsoft.ProgramSynthesis.Features;

namespace FlashGPT3
{
    public class RankingScore : Feature<double>
    {

        private CompositeRankingScore _preciseRanking;

        // initialise precise ranking
        public RankingScore(Grammar grammar) : base(grammar, "Score")
        {
            _preciseRanking = new CompositeRankingScore(grammar);
        }

        protected override double GetFeatureValueForVariable(VariableNode variable) => 0;

        // Combine the scores of the substrings.
        [FeatureCalculator(nameof(Semantics.Concat), Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator(nameof(Semantics.SubStr), Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator(nameof(Semantics.SemMap), Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator(nameof(Semantics.SemPos), Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator(nameof(Semantics.AbsPos), Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator(nameof(Semantics.RegPos), Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator(nameof(Semantics.ConstStr), Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator("k", Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator("m", Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator("r", Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator("q", Method = CalculationMethod.FromProgramNode)]
        [FeatureCalculator("d", Method = CalculationMethod.FromProgramNode)]
        public double ScoreProgram(ProgramNode p)
        {
            // get precise ranking
            ProgramInfo info = _preciseRanking.Calculate(p, null);
            // average score over concats
            double average = info.score; // / (info.concats + 1.0);
            // get number of characters
            int nSemChars = CountSemanticCharacters(info);
            int nPosQueries = info.CountQueries("pos");
            return average + (1000000.0 / (nSemChars + 1.0))
                           + (10000.0 / (nPosQueries + 1.0));

        }

        /// <summary>
        /// Count how many characters in the output are semantic.
        /// </summary>
        public static int CountSemanticCharacters(ProgramInfo info)
        {
            var maps = info.QueriesOfType("map");
            return maps.Select(q => q.Select(e => (e.Item2 ?? "").Length)
                                     .Sum())
                       .Sum();
        }

    }
}