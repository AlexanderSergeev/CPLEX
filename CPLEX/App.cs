using System;
using System.Diagnostics;
using MaxClique;
using CPLEX;

public class App
{
    public static void Main(string[] args)
    {
        var timer = Stopwatch.StartNew();
        var fileName = "johnson16-2-4.clq.txt";
        Console.WriteLine(fileName);
        var graph = GraphParser.ParseNewGraph(fileName);
        var algorithm = new CplexSolver(graph);
        var result = algorithm.FindMaxClique();
        Console.WriteLine(timer.Elapsed);
        Console.WriteLine(result.Count);
        Console.WriteLine(string.Join(",", result));
        Console.ReadKey(false);
    }
}