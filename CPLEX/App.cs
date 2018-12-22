using System;
using System.Diagnostics;
using MaxClique;
using CPLEX;
using System.Collections.Generic;

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
        var colors = algorithm.FindMaxColorSets();
        Console.WriteLine(timer.Elapsed);
        Console.WriteLine(colors.Count);
        var result = new List<string>();
        foreach (var set in colors)
        {
            result.Add(string.Join(",", set.Value));
        }
        Console.Write(string.Join(";", result));
    }
}