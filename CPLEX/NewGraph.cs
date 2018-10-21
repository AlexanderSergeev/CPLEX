using System.Collections.Generic;

namespace CPLEX
{
    public class NewGraph
    {
        private Dictionary<int, GraphNode> nodes;

        public NewGraph()
        {
            nodes = new Dictionary<int, GraphNode>();
        }

        public Dictionary<int, GraphNode> GetNodes()
        {
            return nodes;
        }

        public void CreateEdge(int index1, int index2)
        {
            GraphNode node1 = GetOrCreateNode(index1);
            GraphNode node2 = GetOrCreateNode(index2);

            node1.AddNeighbour(node2);
            node2.AddNeighbour(node1);
        }

        private GraphNode GetOrCreateNode(int index)
        {
            GraphNode node;
            if (!nodes.TryGetValue(index, out node))
            {
                node = new GraphNode(index);
                nodes.Add(index, node);
            }

            return node;
        }
    }
}
