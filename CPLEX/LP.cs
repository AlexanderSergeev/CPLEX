using System;
using System.Collections.Generic;
using System.Linq;
using MaxClique;

public class LP
{
	public static void Main(string[] args)
	{
		var graph = GraphParser.Parse("c-fat200-2 (copy).clq");
		var removedVertices = new List<int>();
		var reducedGraph = graph;
		var minVertex = graph.GetMinVertex();
		var clique = new List<int>(new []{minVertex});
		var upperBound = 1 + graph.GetNeighbours(minVertex).Length;
		while (reducedGraph.Any())
		{
			reducedGraph = reducedGraph.RemoveVertex(minVertex);
			minVertex = reducedGraph.GetMinVertex();
			if(1 + reducedGraph.GetNeighbours(minVertex).Length > upperBound)
			removedVertices.Add(minVertex);
		}
		Console.ReadKey(false);
	}
}