using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.ProgramSynthesis;
using Microsoft.ProgramSynthesis.AST;
using Microsoft.ProgramSynthesis.Features;

namespace FlashGPT3
{
    public class CompositeRankingScore : Feature<ProgramInfo>
    {
        public CompositeRankingScore(Grammar grammar) : base(grammar, "CompositeScore") { }

        protected override ProgramInfo GetFeatureValueForVariable(VariableNode variable) => new ProgramInfo(0.0);

        // Add the scores of the substrings and combine the queries.
        [FeatureCalculator(nameof(Semantics.Concat))]
        public static ProgramInfo ScoreConcat(ProgramInfo a, ProgramInfo b)
        {
            // build from children
            ProgramInfo info = new(a.score + b.score - 500,
                                   a.queries.Concat(b.queries));
            // add concats
            info.concats += (a.concats + b.concats) + 1;
            return info;
        }


        // Multiply scores and combine queries
        [FeatureCalculator(nameof(Semantics.SubStr))]
        public static ProgramInfo ScoreSubStr(ProgramInfo x, ProgramInfo a, ProgramInfo b) =>
            new ProgramInfo(a.score * b.score, a.queries.Concat(b.queries));


        // Higher score for longer strings, see MScore.
        [FeatureCalculator(nameof(Semantics.ConstStr))]
        public static ProgramInfo Score_ConstStr(ProgramInfo c) =>
            new ProgramInfo(c.score);

        // Longer constants is better
        [FeatureCalculator("m", Method = CalculationMethod.FromLiteral)]
        public static ProgramInfo MScore(string m) =>
            new ProgramInfo(m.Length);


        // Prefer absolute positions to regex positions.
        [FeatureCalculator(nameof(Semantics.AbsPos))]
        public static ProgramInfo AbsPosScore(ProgramInfo x, ProgramInfo k) =>
            new ProgramInfo(20.0 + k.score);


        // Combine r and k to be between 4 and 6.
        [FeatureCalculator(nameof(Semantics.RegPos))]
        public static ProgramInfo RegPosScore(ProgramInfo x, ProgramInfo r1, ProgramInfo r2, ProgramInfo k) =>
            new ProgramInfo(5.0 + r1.score * r2.score * k.score,
                            r1.queries.Concat(r2.queries));

        // Favor symbols over words as they provide stronger
        // syntactic information.
        [FeatureCalculator("r", Method = CalculationMethod.FromLiteral)]
        public static ProgramInfo RegexScore(Regex r)
        {
            int l = r.ToString().Length;
            if (l == 0)
                return new ProgramInfo(1.0);
            else if (l > 4)
                return new ProgramInfo(2.0);
            return new ProgramInfo(3.0);
            //return new ProgramInfo((r.ToString().Length > 4) ? 1.0 : 2.0); // TODO: rank regex
        }

        // Positions towards begin and end are better.
        [FeatureCalculator("k", Method = CalculationMethod.FromLiteral)]
        public static ProgramInfo KScore(int k)
            => (k > 0) ? new ProgramInfo(1.0 / (Math.Abs(k) + 0.5)) :
                         new ProgramInfo(1.0 / (Math.Abs(k) + 1.0));

        // Turn into map query
        [FeatureCalculator(nameof(Semantics.SemMap))]
        public static ProgramInfo SemMapScore(ProgramInfo x, ProgramInfo q) =>
            new ProgramInfo(q.score, new QueryInfo(q.queries.First().query, "map"));

        // Turn into pos query
        [FeatureCalculator(nameof(Semantics.SemPos))]
        public static ProgramInfo SemPosScore(ProgramInfo x, ProgramInfo q, ProgramInfo d) =>
            new ProgramInfo(1.0, new QueryInfo(q.queries.First().query, "pos"));

        // Pack the query
        [FeatureCalculator("q", Method = CalculationMethod.FromLiteral)]
        public static ProgramInfo QScore(Tuple<string, string>[] q) =>
            new ProgramInfo(1.0, new QueryInfo(q));

        // Doesn't matter whether we extract left or right
        [FeatureCalculator("d", Method = CalculationMethod.FromLiteral)]
        public static ProgramInfo DScore(string m) =>
            new ProgramInfo(1.0);

    }

}

/// <summary>
/// Keep track of the score and queries.
/// </summary>
public class ProgramInfo
{

    public double score; // score of syntactic part
    public int concats;  // number of concats
    public List<QueryInfo> queries; // queries made

    public ProgramInfo(double s)
    {
        score = s;
        queries = new List<QueryInfo>(0);
        concats = 0;
    }

    public ProgramInfo(double s, QueryInfo q)
    {
        score = s;
        queries = new List<QueryInfo>(0);
        AddQuery(q);
    }

    public ProgramInfo(double s, IEnumerable<QueryInfo> qs)
    {
        score = s;
        // add one by one and remove duplicates
        queries = new List<QueryInfo>(0);
        foreach (QueryInfo q in qs)
            AddQuery(q);
    }

    public bool HasQuery(QueryInfo q) => queries.Any(p => p.Equals(q));
    public void AddQuery(QueryInfo q)
    {
        if (!HasQuery(q))
            queries.Add(q);
    }

    public int CountQueries() => queries.Distinct(new QueryComparer())
                                        .Count();

    public int CountQueries(string type) => queries.Where(q => q.type == type)
                                                   .Distinct(new QueryComparer())
                                                   .Count();


    public IEnumerable<Tuple<string, string>[]> QueriesOfType(string type) =>
        queries.Where(q => q.type == type)
               .Select(q => q.query);

    public int CountExamples()
    {
        if (queries.Count > 0)
            return queries.First().query.Length;
        return 0;
    }

}

/// <summary>
/// Keep track of information about a query.
/// </summary>
public class QueryInfo
{

    public Tuple<string, string>[] query;
    public string type;

    public QueryInfo(Tuple<string, string>[] query)
    {
        this.query = query;
    }

    public QueryInfo(Tuple<string, string>[] query, string type)
    {
        this.query = query;
        this.type = type;
    }

    public bool SameQuery(QueryInfo other) => query.SequenceEqual(other.query);
    public bool Equals(QueryInfo other) => SameQuery(other) && (type == other.type);

}

public class QueryComparer : IEqualityComparer<QueryInfo>
{
    public bool Equals(QueryInfo x, QueryInfo y) => x.SameQuery(y);
    public int GetHashCode(QueryInfo obj) => obj.GetHashCode();
}