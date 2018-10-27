using System;
using System.Diagnostics;
using System.Linq;
using MaxClique;
using CPLEX;

public class App
{
    public static void Main(string[] args)
    {
        var graph = GraphParser.ParseNewGraph("brock200_2.clq");
        var algorithm = new CplexSolver(graph);
        var timer = Stopwatch.StartNew();
        var result = algorithm.FindMaxClique();
        result.All(x => { Console.WriteLine(x.ToString()); return true; });
        Console.WriteLine($"Calls: {algorithm.CallsCount}. Done in {timer.Elapsed}");
        Console.ReadKey(false);
    }
}