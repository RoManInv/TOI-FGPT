using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ProgramSynthesis;
using Microsoft.ProgramSynthesis.AST;
using Microsoft.ProgramSynthesis.Compiler;
using Microsoft.ProgramSynthesis.Learning;
using Microsoft.ProgramSynthesis.Learning.Logging;
using Microsoft.ProgramSynthesis.Learning.Strategies;
using Microsoft.ProgramSynthesis.Specifications;
using Microsoft.ProgramSynthesis.VersionSpace;
using Microsoft.ProgramSynthesis.Features;
using System.Reflection;

namespace FlashGPT3
{
    internal static class LearningUtils
    {

        public static string ResolveFilename(string filename)
        {
            return File.Exists(filename) ?
                  filename
                : Path.Combine(Directory.GetCurrentDirectory(), filename);
        }

        public static Grammar LoadGrammar(string name = "FlashGPT3.grammar") =>
            LoadGrammar(name,
                        CompilerReference.FromAssemblyFiles(typeof(Semantics).GetTypeInfo().Assembly));

        public static Grammar LoadGrammar(string grammarFile, IReadOnlyList<CompilerReference> assemblyReferences)
        {
            var compilationResult = DSLCompiler.Compile(new CompilerOptions()
            {
                InputGrammarText = File.ReadAllText(ResolveFilename(grammarFile)),
                References = assemblyReferences
            });
            if (compilationResult.HasErrors)
            {
                WriteColored(ConsoleColor.Magenta, compilationResult.TraceDiagnostics);
                return null;
            }
            if (compilationResult.Diagnostics.Count > 0)
            {
                WriteColored(ConsoleColor.Yellow, compilationResult.TraceDiagnostics);
            }
            return compilationResult.Value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="examples"></param>
        /// <returns></returns>
        public static Dictionary<State, IEnumerable<object>> Intersect(Dictionary<State, IEnumerable<object>> examples)
        {
            var values = examples.Values.ToList();
            var intersection = values
                .Skip(1)
                .Aggregate(
                    new List<object>(values.First()),
                    (h, e) => h.Intersect(e).ToList()
                );
            return examples.ToDictionary(k => k.Key, k => (IEnumerable<object>)intersection);
        }

        public static void WriteColored(ConsoleColor color, object obj)
            => WriteColored(color, () => Console.WriteLine(obj));

        public static void WriteColored(ConsoleColor color, Action write)
        {
            ConsoleColor currentColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            write();
            Console.ForegroundColor = currentColor;
        }

    }
}
