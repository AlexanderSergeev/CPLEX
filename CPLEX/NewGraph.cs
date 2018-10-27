using System.Collections.Generic;
using System.Linq;

namespace CPLEX
{
    public class NewGraph
    {
        public List<GraphNode> Nodes { get; }

        public NewGraph()
        {
            Nodes = new List<GraphNode>();
        }

        public void CreateEdge(int index1, int index2)
        {
            GraphNode node1 = GetOrCreateNode(index1);
            GraphNode node2 = GetOrCreateNode(index2);
            node1.Neighbours.Add(node2);
            node2.Neighbours.Add(node1);
        }

        private GraphNode GetOrCreateNode(int index)
        {
            if (Nodes.All(node => node.Index != index))
            {
                Nodes.Add(new GraphNode(index));
            }

            return Nodes.First(node => node.Index == index);
        }
    }
}
