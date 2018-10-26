using System;
using System.Linq;
using MaxClique;
using CPLEX;

public class App
{
    public static void Main(string[] args)
    {
        var graph = GraphParser.ParseNewGraph("c-fat200-2 (copy).clq");
        var upperBound = 0;
        var algorithm = new CplexSolver(graph); 
        var result = algorithm.FindMaxClique(upperBound);
        result.All(x => { Console.WriteLine(x.ToString()); return true; });
        Console.ReadKey(false);
    }
}