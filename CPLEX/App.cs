using System;
using System.Diagnostics;
using MaxClique;
using CPLEX;

public class App
{
    public static void Main(string[] args)
    {
        var fileName = "C125.9.clq.txt";
        Console.WriteLine("executionTime, cliqueCount, clique");
        var graph = GraphParser.ParseNewGraph(fileName);
        var algorithm = new CplexSolver(graph);
        var timer = Stopwatch.StartNew();
        var result = algorithm.FindMaxClique();
        Console.WriteLine(timer.Elapsed);
        Console.WriteLine(result.Count);
        Console.WriteLine(string.Join(",", result));
        Console.ReadKey(false);
    }
}