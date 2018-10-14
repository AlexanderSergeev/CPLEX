using System;
using System.IO;
using System.Linq;

namespace MaxClique
{
	public static class GraphParser
	{
		public static Graph Parse(string fileName)
		{
			var lines = File.ReadAllLines(fileName);
			var edges = lines
				.Where(s => s.StartsWith("e", StringComparison.Ordinal))
				.Select(s =>
			{
				var vertices = s.Split(' ').Skip(1).Select(int.Parse);
				return new Edge(vertices.First(), vertices.Last());
			}).ToArray();
			return new Graph(edges);
		}
	}
}
