using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MaxClique
{
	public struct Graph : IEnumerable<Edge>
    {
		public HashSet<Edge> Edges { get; }

		public Graph(Edge[] edges)
        {
			Edges = new HashSet<Edge>(edges);
        }

		public override string ToString()
		{
			return string.Join("\n", Edges);
		}
        
		public IEnumerator<Edge> GetEnumerator()
		{
			return Edges.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
        
		public int[] GetNeighbours(int vertex){
			return Edges
				.Where(e => e.From == vertex || e.To == vertex)
				.Select(e => e.From == vertex ? e.To : e.From)
				.ToArray();
		}

		public int[] GetVertices(){
			return new HashSet<int>(
				Edges.SelectMany(e => new[] { e.From, e.To }))
					.ToArray();
		}
        
		public int GetMinVertex(){
			var graph = this;
			return GetVertices().Min(x => graph.GetNeighbours(x).Length);
		}

		public Graph RemoveVertex(int vertex){
			var edges = Edges
				.Where(e => e.From != vertex && e.To != vertex)
				.ToArray();
			return new Graph(edges);
		}
	}
}
