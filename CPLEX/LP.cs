using System;
using System.Collections.Generic;
using System.Linq;
using MaxClique;

public class LP
{
	/*public static void Main(string[] args)
	{
		var graph = GraphParser.Parse("c-fat200-2 (copy).clq");
		var removedVertices = new List<int>();
		var result = MaxClique(graph, new int[0]);
		result.All(x => { Console.WriteLine(x); return true; });
		Console.ReadKey(false);
	}*/

	static int[] MaxClique(Graph graph, int[] clique)
	{
		var vertices = graph.GetVertices();
		if (!vertices.Any())
		{
			if (clique.Length > maxClique.Length)
				maxClique = clique;
			return maxClique;
		}
		while (vertices.Length + clique.Length < maxClique.Length)
			continue;
		var minVertex = vertices.Min();
		var neighbors = graph.GetNeighbours(minVertex);
		graph = graph.RemoveVertex(minVertex);
		foreach (var edge in graph)
		{
			if (!neighbors.Contains(edge.From))
				graph = graph.RemoveVertex(edge.From);
			if (!neighbors.Contains(edge.To))
				graph = graph.RemoveVertex(edge.To);
		}
		return MaxClique(graph,
		                 clique
		                 .Concat(new[] { minVertex })
		                 .ToArray());
	}


		static int[] maxClique = new int[0];
}