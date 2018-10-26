using System.Collections.Generic;

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

        public string GetNeighboursString()
        {
            string s="";
            foreach(var n in neighbours)
            {
                s = s +","+ n.index.ToString();
            }
            return s;
        }

        public override string ToString()
        {
            return index.ToString() + "-neigbours:" + GetNeighboursString();
        } 
    }
}

