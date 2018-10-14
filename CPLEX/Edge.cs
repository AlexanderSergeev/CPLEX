using System;

namespace MaxClique
{
	public struct Edge : IEquatable<Edge>
    {
		public int From { get; }
		public int To { get; }

		public Edge(int from, int to)
		{
			From = from;
			To = to;
		}

		public override string ToString()
		{
			return $"{From}->{To}";
		}

		public override bool Equals(object obj)
		{
			if (base.Equals(obj)) return true;
			if (!(obj is Edge)) return false;
			return Equals((Edge)obj);
		}

		public bool Equals(Edge other)
		{
			return From == other.From && To == other.To ||
								From == other.To && To == other.From;
		}
	}
}