using System;
using System.Diagnostics;
using MaxClique;
using CPLEX;
using System.IO;
using System.Threading.Tasks;

public class App
{
    private static object locker = new object();
    public static void Main(string[] args)
    {
        using (var writer = new StreamWriter("D:/VSProjects/CPLEX/stats.csv") { AutoFlush = true })
        {
            var fileNames = new[] { "c-fat500-1.clq.txt", "c-fat500-2.clq.txt", "c-fat500-5.clq.txt", "c-fat200-5.clq.txt", "C125.9.clq.txt", "johnson16-2-4.clq.txt", "hamming8-4.clq.txt", "brock200_3.clq.txt", "brock200_4.clq.txt", "keller4.clq.txt" };
            Parallel.ForEach(fileNames,
                fileName =>
                {
                    writer.WriteLine(fileName);
                    var timer = Stopwatch.StartNew();
                    var graph = GraphParser.ParseNewGraph(fileName);
                    var algorithm = new CplexSolver(graph);
                    var result = algorithm.FindMaxClique();
                    lock (locker)
                    {
                        writer.WriteLine(fileName);
                        writer.WriteLine(timer.Elapsed);
                        writer.WriteLine(result.Count);
                        writer.WriteLine(string.Join(",", result));
                    }
                });
        }
        Console.WriteLine("Done");
        Console.ReadKey(false);
    }
}