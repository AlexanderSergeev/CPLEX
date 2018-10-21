using CPLEX;
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

        public static NewGraph ParseNewGraph(string fileName)
        {
            NewGraph graph = new NewGraph();
            var lines = File.ReadAllLines(fileName);
            foreach (var line in lines)
            {
                if (line.StartsWith("e", StringComparison.Ordinal))
                {
                    var vertices = line.Split(' ').Skip(1).Select(int.Parse);
                    graph.CreateEdge(vertices.First(), vertices.Last());
                }
            }
            return graph;
        }
    }
}
