using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Models;

namespace FlashGPT3
{
    public static class EmbeddingQueryRunner
    {

        internal static double temperature = 0.1;
        internal static OpenAI_API.OpenAIAPI api = new OpenAI_API.OpenAIAPI(apiKeys: getAPI());
        internal static Query builder = new ShortQuery();
        private static Dictionary<string, Dictionary<double, List<OpenAI_API.Embedding.Data>>> _cache = new();


        public static string? getAPI()
        {
            string? api;
            try
            {
                //Pass the file path and file name to the StreamReader constructor
                StreamReader sr = new StreamReader("../../../openaikey.txt");
                //Read the first line of text
                api = sr.ReadLine();
                sr.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
                api = null;
            }
            return api;
        }
        /// <summary>
        /// Run a query that consists of a background and a question.
        ///
        /// Uses the default <c>QueryRunner.builder</c> and <c>QueryRunner.api</c>.
        /// </summary>
        /// <param name="background">Examples for few-shot learning a function f(x).</param>
        /// <param name="question">The input to give to the learned function.</param>
        /// <param name="forceInput">Whether to force the output to be a substring of the input.</param>
        /// <returns></returns>
        public static List<OpenAI_API.Embedding.Data>? Run(string text,
                                                      bool? forceInput = null)
        {
            OpenAI_API.Embedding.EmbeddingRequest query = new OpenAI_API.Embedding.EmbeddingRequest(Model.AdaTextEmbedding, text);
            if ((!_cache.ContainsKey(text) ||
                 !_cache[text].ContainsKey(temperature)) &&
                api.Auth != null)
            {
                // ensure query in cache
                if (!_cache.ContainsKey(text))
                    _cache[text] = new Dictionary<double, List<OpenAI_API.Embedding.Data>>(1);
                // check if temperature exists
                var task = api.Embeddings.CreateEmbeddingAsync(query);
                // save result to cache
                _cache[text][temperature] = task.Result.Data.ToList();
            }
            // nothing to return
            if (!_cache.ContainsKey(text))
                return null;

            return _cache[text][temperature];

        }

        /// <summary>
        /// Detect whether the output is a substring of the input.
        /// </summary>
        /// <param name="query">Query</param>
        /// <returns></returns>
        public static bool ConstrainOutput(Tuple<string, string>[] query)
        {
            return query.All(t => t.Item1.Contains(t.Item2));
        }

        /// <summary>
        /// Load data into static cache.
        /// </summary>
        /// <param name="file">Filename of json cache.</param>
        public static void LoadCache(string file, bool reset = false)
        {
            // reset cache
            if (reset)
                _cache = new Dictionary<string, Dictionary<double, List<OpenAI_API.Embedding.Data>>>();
            // load from JSON file
            if (File.Exists(file))
            {
                Dictionary<string, Dictionary<double, List<OpenAI_API.Embedding.Data>>> rawData =
                    JsonConvert.DeserializeObject<Dictionary<string, Dictionary<double, List<OpenAI_API.Embedding.Data>>>>(
                        File.ReadAllText(file)
                    )!;
                // add to the cache
                foreach (var pair in rawData)
                    if (!_cache.ContainsKey(pair.Key))
                        _cache.Add(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Save the cache.
        /// </summary>
        /// <param name="file"></param>
        public static void SaveCache(string file)
        {
            if (!Directory.GetParent(file).Exists)
                Directory.GetParent(file).Create();
            File.WriteAllText(file, JsonConvert.SerializeObject(_cache,
                                                                Newtonsoft.Json.Formatting.Indented));
        }

    }

    /// <summary>
    /// Abstract query class.
    /// </summary>
    public abstract class EmbeddingQuery
    {

        internal static string template = "Q: {0}\nA: {1}";

        public string Generate(Tuple<string, string>[] background, string question)
        {
            var data = background.Append(Tuple.Create(question, ""));
            return "Transformations:\n\n" +
                   String.Join("\n\n",
                               data.Select(t => String.Format(template,
                                                              t.Item1, t.Item2)))
                         .TrimEnd();
        }
    }

    /// <summary>
    /// Use short QA format.
    /// </summary>
    public class ShortEmbeddingQuery : Query
    {

    }

    /// <summary>
    /// Use long QA format.
    /// </summary>
    public class LongEmbeddingQuery : Query
    {
        internal static new string template = "Question: {0}\nAnswer: {1}";
    }

    /// <summary>
    /// Use arrow query.
    /// </summary>
    public class ArrowEmbeddingQuery : Query
    {
        internal static new string template = "{0} => {1}";
    }
}
