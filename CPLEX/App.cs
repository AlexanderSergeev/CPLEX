using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MaxClique;
using CPLEX;

public class App
{
    private static object locker = new object();
    public static void Main(string[] args)
    {
        using (var writer = new StreamWriter("stats.csv") {AutoFlush = true})
        {
            writer.WriteLine("fileName, cliqueCount, executionTime");
            var fileNames = Directory.EnumerateFiles(Directory.GetCurrentDirectory()).Where(n => n.Contains("clq"))
                .ToArray();
            Parallel.ForEach(fileNames,
                fileName =>
                {
                    var graph = GraphParser.ParseNewGraph(fileName);
                    var algorithm = new CplexSolver(graph);
                    var timer = Stopwatch.StartNew();
                    var result = algorithm.FindMaxClique();
                    lock (locker)
                    {
                        writer.WriteLine(string.Join(", ", fileName, result.Count, timer.Elapsed));
                    }
                });
        }
        Console.WriteLine("Done");
        Console.ReadKey(false);
    }
}