using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.ProgramSynthesis;
using Microsoft.ProgramSynthesis.Specifications;
using Microsoft.ProgramSynthesis.Features;
using Microsoft.ProgramSynthesis.Learning;
using Microsoft.ProgramSynthesis.Learning.Strategies;
using Microsoft.ProgramSynthesis.VersionSpace;
using Microsoft.ProgramSynthesis.AST;
using Microsoft.ProgramSynthesis.Transformation.Text;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.ProgramSynthesis.Wrangling;
using AngleSharp.Common;
using Microsoft.ProgramSynthesis.Utils;
using Microsoft.ProgramSynthesis.Conditionals.Build.RuleNodeTypes;

namespace FlashGPT3
{
    public class BenchmarkRunner
    {
        public static void Main()
        {

            string[] semantic = BenchmarkUtils.GetBenchmarks("../../../../benchmarks/IT/");

            // flashgpt3 with clustering
            WitnessFunctions.Clustering = true;
            Run(semantic, new BenchmarkSettings(grammarName: "FlashGPT3",
                                                temperature: 0.0,
                                                query: "llamashort",
                                                outputDir: Path.Combine(BenchmarkUtils.DefaultOutputDir,
                                                                        "artefacts_flashgpt3_cluster")));

            Console.WriteLine($"Written files to {BenchmarkUtils.DefaultOutputDir}");

        }

        public static void Run(string[] files, BenchmarkSettings settings)
        {
            BenchmarkUtils.debug = false;
            BenchmarkResult result;
            // load cache
            OpenAIQueryRunner.LoadCache(settings.cacheFile);
            foreach (string inputFile in files)
            {
                Console.WriteLine($">>> {Path.GetFileNameWithoutExtension(inputFile)}");

                // generate output file
                string outputFile = Path.Combine(settings.outputDir,
                                                 string.Format("{0}_{1}.json",
                                                               settings.Description(),
                                                               Path.GetFileNameWithoutExtension(inputFile)));
                // load benchmark
                Benchmark benchmark = new Benchmark(inputFile, settings);
                // run it and print result
                result = benchmark.Run();
                result.PrintResult();
                // write result to file
                File.WriteAllText(outputFile,
                                  JsonConvert.SerializeObject(result,
                                                              Formatting.Indented));
                // save cache after each iteration
                OpenAIQueryRunner.SaveCache(settings.cacheFile);

            }
        }
        public static void RunFlashFill(string[] files, SyntacticBenchmarkSettings settings)
        {
            BenchmarkUtils.debug = false;
            BenchmarkResult result;
            Session session;
            Program program;
            foreach (string inputFile in files)
            {
                Console.WriteLine($">>> {Path.GetFileNameWithoutExtension(inputFile)}");
                // generate output file
                string outputFile = Path.Combine(settings.outputDir,
                                                 string.Format("{0}_{1}.json",
                                                               settings.Description(),
                                                               Path.GetFileNameWithoutExtension(inputFile)));
                // load benchmark
                Benchmark benchmark = new Benchmark(inputFile, settings);
                // run FF one
                result = new BenchmarkResult(settings);
                for (int i = 1; i < Math.Min(settings.maxExamples + 1, benchmark.problem.Count); i++)
                {
                    var examples = benchmark.problem.Take(i).Select(
                        p => new Example(new InputRow(p.Item1.Split(" | ")), p.Item2)
                    );
                    session = new Session();
                    session.Constraints.Add(examples);
                    program = session.Learn();

                    foreach ((string X, string y) in benchmark.problem.Skip(i))
                    {
                        string answer = (string)program.Run(new InputRow((X ?? "").Split(" | "))) ?? "";
                        result.AddResult(i, answer, y ?? "");
                    }
                    //if (result.Accuracy(i) == 1.0)
                    //    break;
                }
                // run it and print result
                result.PrintResult();
                // write result to file
                File.WriteAllText(outputFile,
                                  JsonConvert.SerializeObject(result,
                                                              Formatting.Indented));
            }
        }

    }

    /// <summary>
    /// Encapsulate the settings of an experiment.
    /// </summary>
    public class BenchmarkSettings
    {

        public string grammarName;

        /// <summary>
        /// Number of programs that will be learned.
        /// </summary>
        public int k;

        /// <summary>
        /// Query to use.
        /// </summary>
        public string query = "long";

        /// <summary>
        /// Temperature for GPT-3.
        /// </summary>
        public double temperature = 0.2;

        /// <summary>
        /// Max number of examples to use.
        /// </summary>
        public int maxExamples = 5;

        /// <summary>
        /// Cache used for learning.
        /// </summary>
        [JsonIgnore]
        public string cacheFile = BenchmarkUtils.DefaultCacheFile;

        /// <summary>
        /// Directory for output. Will write one JSON file per experiment
        /// to this directory.
        /// </summary>
        [JsonIgnore]
        public string outputDir = BenchmarkUtils.DefaultOutputDir;

        [JsonIgnore]
        public Grammar grammar;
        [JsonIgnore]
        public Feature<double> scorer;
        [JsonIgnore]
        public Feature<ProgramInfo> detailer;

        public BenchmarkSettings(string cacheFile = null,
                                 string outputDir = null,
                                 int k = 1,
                                 string query = "long",
                                 string grammarName = "FlashGPT3",
                                 double temperature = 0.2)
        {
            grammar = LearningUtils.LoadGrammar(grammarName + ".grammar");
            scorer = new RankingScore(grammar);
            detailer = new CompositeRankingScore(grammar);
            // settings
            this.cacheFile = cacheFile ?? this.cacheFile;
            this.outputDir = outputDir ?? this.outputDir;
            this.k = k;
            this.query = query;
            this.grammarName = grammarName;
            this.temperature = temperature;

            // ensure exists
            new FileInfo(this.cacheFile).Directory.Create();
            new DirectoryInfo(this.outputDir).Create();
        }

        /// <summary>
        /// Create engine.
        /// </summary>
        [JsonIgnore]
        public SynthesisEngine Engine =>
            new SynthesisEngine(grammar, new SynthesisEngine.Config
            {
                Strategies = new ISynthesisStrategy[]
                        {
                            new EnumerativeSynthesis(),
                            new DeductiveSynthesis(new WitnessFunctions(grammar))
                        },
                UseThreads = false
            });

        /// <summary>
        /// Get the query builder.
        /// </summary>
        /// <returns></returns>
        public Query Builder()
        {
            if (query == "short")
                return new ShortQuery();
            if (query == "arrow")
                return new ArrowQuery();
            return new LongQuery();
        }

        public string Description() => $"{grammarName.ToLower()}_{query.ToLower()}_{temperature}";

    }

    public class SyntacticBenchmarkSettings : BenchmarkSettings
    {
        public SyntacticBenchmarkSettings(string outputDir = null)
        {
            grammar = null;
            scorer = null;
            detailer = null;
            this.cacheFile = null;
            this.outputDir = outputDir ?? this.outputDir;
            this.k = 1;
            this.query = null;
            this.grammarName = null;
            this.temperature = 0.0;
            // ensure exists
            new DirectoryInfo(this.outputDir).Create();
        }

        public new string Description() => $"flashfill";

    }

    public class Benchmark
    {

        public List<Tuple<string, string>> problem;
        public BenchmarkSettings settings;

        public Benchmark(string problemFile, BenchmarkSettings settings)
        {
            if (problemFile.EndsWith(".csv"))
                this.problem = BenchmarkUtils.ReadProblem(problemFile);
            else if (problemFile.EndsWith(".json"))
                this.problem = BenchmarkUtils.ReadProblemJson(problemFile);
            this.settings = settings;
        }

        /// <summary>
        /// Run benchmark.
        /// </summary>
        /// <returns>A mapping of number of examples required to a list of
        /// truth values for the example.</returns>
        /// 

        internal void SaveTOICache(ExampleSpec spec, string path, bool learning = false)
        {
            Dictionary<string, Tuple<string, string>> ToiDict;
            Dictionary<string, Tuple<string, string>> currTOIDict;
            Dictionary<string, float[]> observedTOI = new();
            Dictionary<string, string> examples = new Dictionary<string, string>();
            foreach (var item in spec.Examples)
            {
                string src = (string)item.Key.Bindings.Select(p => p.Value).FirstOrDefault();
                string tgt = item.Value.ToString();
                if(!examples.Keys.Contains(src)) examples.Add(src, tgt);
            }
            TOIIdentSim toi = new TOIIdentSim();
            if (learning)
            {
                if (File.Exists(path))
                {
                    if (new FileInfo(path).Length == 0)
                        ToiDict = new();
                    ToiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(path));
                }
                else
                {
                    File.Create(path).Close();
                    ToiDict = new();
                }
                Dictionary<string, string> ExamplePairs = new Dictionary<string, string>();
                foreach (var item in spec.Examples)
                {
                    string src = (string)item.Key.Bindings.Select(p => p.Value).FirstOrDefault();
                    string tgt = item.Value.ToString();
                    ExamplePairs.Add(src, tgt);
                    //if (!ToiDict.Keys.Contains(src))
                    //{
                    //    (Tuple<string, string> currtoi, observedTOI) = toi.GetTOI(src, tgt, observedTOI);
                    //    ToiDict[src] = currtoi;
                    //}
                }

                currTOIDict = toi.GetAllTOIs(ExamplePairs);
                foreach (var kvp in currTOIDict)
                {
                    if(ToiDict.Keys.Contains(kvp.Key)) ToiDict[kvp.Key] = kvp.Value;
                    else ToiDict.Add(kvp.Key, kvp.Value);
                }
            }
            else
            {
                if (File.Exists(path))
                {
                    if (new FileInfo(path).Length == 0)
                        ToiDict = new();
                    ToiDict = JsonConvert.DeserializeObject<Dictionary<string, Tuple<string, string>>>(File.ReadAllText(path));
                }
                else
                {
                    File.Create(path).Close();
                    ToiDict = new();
                }
            }
            //else
            //{

            //}
            File.WriteAllText(path, JsonConvert.SerializeObject(ToiDict, Formatting.Indented));
        }

        public BenchmarkResult Run()
        {

            const string TOICache = @"../../../../FlashGPT3/cache/toi.json";
            

            Stopwatch stopwatch = new Stopwatch();
            BenchmarkResult result = new BenchmarkResult(this.settings);
            // set the query mode
            OpenAIQueryRunner.builder = settings.Builder();
            OpenAIQueryRunner.temperature = settings.temperature;
            // run benchmark
            for (int i = 1; i < Math.Min(settings.maxExamples + 1, problem.Count); i++)
            {
                ExampleSpec spec = MakeExamples(i);

                


                // set to oracle mode for learning and start clock
                Semantics.learning = true;
                Semantics.learningCalls = 0;
                stopwatch.Restart();
                bool TOILearning = false;
                SaveTOICache(spec, TOICache, TOILearning);
                var task = Task.Run(() => 
                    settings.Engine.LearnGrammarTopK(spec, settings.scorer, k: 10));
                // if task finished on time, get results
                if (task.Wait(BenchmarkUtils.timeout))
                {
                    Semantics.learning = false;
                    stopwatch.Stop();
                    ProgramSet consistent = task.Result;
                    //Console.WriteLine($"> Finished {i} in {stopwatch.Elapsed.Seconds} seconds.");
                    Console.WriteLine($"> Finished {i} in {stopwatch.Elapsed.TotalMilliseconds} ms.");
                    if (BenchmarkUtils.debug == true)
                        Console.WriteLine($"{i} examples with {Semantics.learningCalls} calls.");
                    // store to result file
                    foreach (ProgramNode p in consistent.RealizedPrograms)
                    {
                        result.AddProgram(i, p);
                        if (BenchmarkUtils.debug == true)
                            Console.WriteLine($"[{p.GetFeatureValue(settings.scorer)}] {p}");
                    }
                    // get best program and add to result
                    ProgramNode best = consistent.TopK(settings.scorer, k: 1).FirstOrDefault();
                    result.AddTime(i, stopwatch.Elapsed.Milliseconds);
                    result.AddCalls(i, Semantics.learningCalls);
                    foreach ((State X, string y) in MakeTests(i))
                    {
                        string answer = (string)(best.Invoke(X) ?? "");
                        result.AddResult(i, answer, y);
                    }
                    //if (result.Accuracy(i) == 1.0)
                    //    break;
                }
                else
                {
                    result.Fail(i);
                    result.AddTime(i, BenchmarkUtils.timeout.TotalMilliseconds);
                    result.AddCalls(i, Semantics.learningCalls);
                    break;
                }

            }
            return result;
        }

        /// <summary>
        /// Generate a specification with a limited number of examples.
        /// </summary>
        /// <param name="n">Number of examples to use.</param>
        /// <returns></returns>
        public ExampleSpec MakeExamples(int n)
        {
            return new ExampleSpec(
                problem.Take(n)
                       .ToDictionary(t => State.CreateForLearning(settings.grammar.InputSymbol, t.Item1),
                                     t => (object)t.Item2));
        }

        /// <summary>
        /// Generate the tests.
        /// </summary>
        /// <param name="n"></param>
        /// <returns>A list of (X, y) pairs with X already encoded as a state.</returns>
        public IEnumerable<Tuple<State, string>> MakeTests(int n)
        {
            List<Tuple<State, string>> tests = new(problem.Count - n);
            for (int i = n; i < problem.Count; i++)
                tests.Add(Tuple.Create(State.CreateForExecution(settings.grammar.InputSymbol,
                                                                problem[i].Item1),
                                       problem[i].Item2));
            return tests;
        }

        public override string ToString() =>
            String.Join("\n", problem.Select(e => String.Format("{0} -> {1}", e.Item1, e.Item2)));

    }

    /// <summary>
    /// Result of running benchmark.
    /// </summary>
    public class BenchmarkResult
    {

        /// <summary>
        /// Settings used to run benchmark.
        /// </summary>
        public BenchmarkSettings settings;

        /// <summary>
        /// Mapping of number of examples to the (answer, truth) tuples
        /// of the testing data for that benchmark..
        /// </summary>
        public SortedDictionary<int, List<Submission>> outcomes;

        /// <summary>
        /// Program used in this benchmark as XML (not human readable).
        /// </summary>
        public Dictionary<int, List<RankedProgram>> programs;

        /// <summary>
        /// Time to learn.
        /// </summary>
        public Dictionary<int, double> times;
        public Dictionary<int, int> calls;

        /// <summary>
        /// Failed programs
        /// </summary>
        public List<int> failed;

        /// <summary>
        /// Initialise result.
        /// </summary>
        /// <param name="examples">Number of examples in this benchmark.</param>
        public BenchmarkResult(BenchmarkSettings benchmarkSettings)
        {
            settings = benchmarkSettings;
            outcomes = new SortedDictionary<int, List<Submission>>();
            programs = new Dictionary<int, List<RankedProgram>>();
            times = new Dictionary<int, double>();
            calls = new Dictionary<int, int>();
            failed = new List<int>();
        }

        /// <summary>
        /// Add a new result.
        /// </summary>
        /// <param name="i">Number of training examples.</param>
        /// <param name="answer">Answer of program.</param>
        /// <param name="truth">Truth.</param>
        public void AddResult(int i, string answer, string truth)
        {
            if (!outcomes.ContainsKey(i))
                outcomes[i] = new List<Submission>();
            outcomes[i].Add(new Submission(answer, truth));
        }

        /// <summary>
        /// Add program.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="program"></param>
        public void AddProgram(int i, ProgramNode program)
        {
            // add new key
            if (!programs.ContainsKey(i))
                programs[i] = new List<RankedProgram>(1);
            // score
            ProgramInfo info = settings.detailer.Calculate(program, null);
            programs[i].Add(new RankedProgram(program.ToString(),
                                              program.GetFeatureValue(settings.scorer),
                                              info.QueriesOfType("map").ToList()));
        }

        /// <summary>
        /// Add time.
        /// </summary>
        public void AddTime(int i, double time)
        {
            times[i] = time;
        }

        public void AddCalls(int i, int calls)
        {
            this.calls[i] = calls;
        }

        /// <summary>
        /// Indicate that some run failed.
        /// </summary>
        public void Fail(int i)
        {
            failed.Add(i);
        }

        /// <summary>
        /// Number of examples in this benchmark.
        /// </summary>
        public int NumberOfExamples() => outcomes.Count + 1;

        /// <summary>
        /// Number of examples required to solve this benchmark.
        /// </summary>
        public int RequiredExamples()
            => outcomes.FirstOrDefault(p => p.Value.All(t => t.Answer.Equals(t.Truth)))
                       .Key;

        /// <summary>
        /// Number of correct examples.
        /// </summary>
        public double Accuracy(int i)
        {
            if (!outcomes.ContainsKey(i))
                return -1;
            if (outcomes[i] == null)
                return -1;
            return (double)outcomes[i].Where(t => t.Answer.Equals(t.Truth)).Count() /
                           outcomes[i].Count;
        }

        /// <summary>
        /// Prints the result.
        /// </summary>
        public void PrintResult()
        {
            int othercounter = 0;

            foreach (KeyValuePair<int, List<RankedProgram>> p in programs)
            {
                Console.WriteLine($">> {p.Key} examples");
                Console.WriteLine($"> Program:  {p.Value.First().Program}");
                Console.WriteLine($"> Score:  {p.Value.First().Score} points");
                Console.WriteLine($"> Time:     {this.times[p.Key]}");
                Console.WriteLine($"> Calls:    {this.calls[p.Key]}");
                Console.WriteLine($"> Accuracy: {this.Accuracy(p.Key)}");
                if(othercounter > 0) Console.WriteLine($"> Other realized programs: ");

                int counter = 0;
                foreach (var prog in p.Value)
                {
                    if (counter >= othercounter) break;
                    Console.WriteLine($" >> {prog.Program}");
                    Console.WriteLine($" >> Score: {prog.Score} points");
                    Console.WriteLine($"-------------");
                    counter++;
                }
                foreach (Submission r in outcomes[p.Key])
                    Console.WriteLine($"  {r.Answer}\t({r.Truth})");
            }
            Console.WriteLine($"> {this.RequiredExamples()} examples required.");
            Console.WriteLine($"===================================");
        }

        public record Submission(string Answer, string Truth);
        public record RankedProgram(string Program,
                                    double Score,
                                    List<Tuple<string, string>[]> Queries);

    }

    /// <summary>
    /// Utilities for experiments.
    /// </summary>
    public class BenchmarkUtils
    {

        public static bool debug = false;
        public static TimeSpan timeout = TimeSpan.FromMinutes(5000);

        public static string DefaultCacheDir =>
            Path.Combine(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName,
                         "cache");

        public static string DefaultCacheFile =>
            Path.Combine(DefaultCacheDir, "default.json");

        public static string DefaultOutputDir =>
            Path.Combine(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName,
                         "results");

        public static List<Tuple<string, string>> ReadProblem(string file)
        {
            using (var reader = new StreamReader(file))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false }))
            {
                var rows = new List<Tuple<string, string>>();
                while (csv.Read())
                    rows.Add(csv.GetRecord<Tuple<string, string>>());
                return rows;
            }
        }

        public static List<Tuple<string, string>> ReadProblemJson(string file)
        {
            List<Tuple<string, string>> problem = new List<Tuple<string, string>>();
            foreach (JObject example in JObject.Parse(File.ReadAllText(file))
                                               .Property("Examples")
                                               .Value)
            {
                string output = (string)example.GetValue("Output");
                string input = "";
                if (example.ContainsKey("Inputs"))
                    input = (string)(example.GetValue("Inputs").First());
                else if (example.ContainsKey("InputRow"))
                    input = String.Join(" | ", example.GetValue("InputRow"));
                else
                    continue;
                problem.Add(Tuple.Create(input, output));
            }
            return problem;
        }

        public static string[] GetBenchmarks(string directory, string extension = "csv")
            => Directory.GetFiles(directory, $"*.{extension}");

        public static string GetName(string file)
            => Path.GetFileNameWithoutExtension(file);

    }

}