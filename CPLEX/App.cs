using System;
using System.Diagnostics;
using MaxClique;
using CPLEX;

public class App
{
    public static void Main(string[] args)
    {
        var fileName = "myciel4.col";
        if (args.Length > 0)
        {
            fileName = args[0];
        }
        Console.WriteLine(fileName);
        var timer = Stopwatch.StartNew();
        var graph = GraphParser.ParseNewGraph(fileName);
        var algorithm = new CplexSolver(graph);
        var result = algorithm.FindMaxColorSets();
        Console.WriteLine(timer.Elapsed);
        Console.WriteLine(result.Count);
        Console.ReadKey(false);
    }
}