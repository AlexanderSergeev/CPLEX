using System.Collections.Generic;
using System.Linq;

namespace CPLEX
{
    public struct GraphNode
    {
        public int Index { get; }
        public List<GraphNode> Neighbours { get; }

        public GraphNode(int index)
        {
            Index = index;
            Neighbours = new List<GraphNode>();
        }

        public override string ToString()
        {
            return $"{Index}\tneigbours:{string.Join(",", Neighbours.Select(n => n.Index))}";
        }
    }
}

