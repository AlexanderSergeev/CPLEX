using System;
using System.Linq;
using MaxClique;
using CPLEX;

public class App
{
    public static void Main(string[] args)
    {
        var graph = GraphParser.ParseNewGraph("D:/VSProjects/CPLEX/CPLEX/C125.9.clq");
        var algorithm = new CplexSolver(graph); 
        var result = algorithm.FindMaxClique();
        result.All(x => { Console.WriteLine(x); return true; });
        Console.WriteLine($"Calls: {algorithm.CallsCount}");
        Console.ReadKey(false);
    }
}