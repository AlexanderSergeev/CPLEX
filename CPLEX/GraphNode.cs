using System.Collections.Generic;
using System.Linq;

namespace CPLEX
{
    public class GraphNode
    {
        private int index;
        private List<GraphNode> neighbours;

        public GraphNode(int index)
        {
            this.index = index;
            neighbours = new List<GraphNode>();
        }

        public void AddNeighbour(GraphNode neighbour)
        {
            neighbours.Add(neighbour);
        }

        public int GetIndex()
        {
            return index;
        }

        public List<GraphNode> GetNeighbours()
        {
            return neighbours;
        }

        public override string ToString()
        {
            return $"{index}\tneigbours:{string.Join(",", neighbours.Select(n => n.index))}";
        }
    }
}

