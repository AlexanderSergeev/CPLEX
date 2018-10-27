using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CPLEX
{
    class Heuristic
    {
        private NewGraph graph;
        private List<GraphNode> maxClique;

        public Heuristic(NewGraph graph)
        {
            this.graph = graph;
            this.maxClique = new List<GraphNode>();
        }

        public List<GraphNode> FindMaxClique()
        {
            List<GraphNode> nodes = new List<GraphNode>(graph.GetNodes().Values);
            Dictionary<int, int> colors = FindChromaticNumber(nodes);
            // Sorting by color number - color number for node shows number of adjacent with each other vertices
            // and we are interested only in nodes with a lot of such neighbours.
            SortByColor(nodes, colors);

            BranchAndBound(nodes, new List<GraphNode>(), colors);
            return GetMaxClique();
        }

        public List<GraphNode> GetMaxClique()
        {
            return maxClique;
        }

        public void BranchAndBound(List<GraphNode> candidates, List<GraphNode> clique, Dictionary<int, int> colors)
        {
            foreach (GraphNode c in candidates)
            {
                // we can predict if node cannot expand current max clique
                if (clique.Count + colors.ElementAt(c.GetIndex()).Value <= GetMaxClique().Count)
                { // |Q|+|R| > |Qmax|
                  // and as nodes are sorted by color number, we don't need to verify further nodes
                  // so just exit this branch
                    return;
                }

                List<GraphNode> currentClique = new List<GraphNode>(clique);
                currentClique.Add(c);

                List<GraphNode> currentCandidates = new List<GraphNode>(candidates);
                // Skip also candidates with smaller index when current one
                // this way we avoid situations like checking both 1-2-3 and 2-3-1 cliques
                currentCandidates = currentCandidates.GetRange(currentCandidates.IndexOf(c), currentCandidates.Count);
                currentCandidates.Intersect(c.GetNeighbours());

                if (currentCandidates.Count > 0)
                {
                    // More candidates found, so continuing
                    Dictionary<int, int> candidateColors = FindChromaticNumber(currentCandidates);
                    SortByColor(currentCandidates, candidateColors);
                    BranchAndBound(currentCandidates, currentClique, candidateColors);
                }
                else if (currentClique.Count > GetMaxClique().Count)
                {
                    maxClique = currentClique;
                }
            }
        }

        /**
     * Implementation based on Tomita and Yamada (1978), Fujii and Tomita (1982), and Tomita et al. (1988)
     * http://www.dcs.gla.ac.uk/~pat/jchoco/clique/indSetMachrahanish/papers/tomita2006.pdf
     * Returns chromatic number for given nodes
     *
     * @param nodes collection of nodes to find chromatic number for
     * @return map contains node_index -> color_number
     */
        private static Dictionary<int, int> FindChromaticNumber(List<GraphNode> nodes)
        {
            // It is better for us to have small color for nodes with a little number of neighbours
            // In this case using |Q|+|R| > |Qmax| we will reject nodes with big number of neighbours
            //nodes.Sort(Comparer.<GraphNode> comparingInt(elem=>elem.getNeighbours().size()).reversed());
            int maxColor = 0;
            // contains sets with vertexes of the same color. Key - color number, value - set of nodes of this color
            Dictionary<int, HashSet<GraphNode>> colorsSets = new Dictionary<int, HashSet<GraphNode>>();
            Dictionary<int, int> colors = new Dictionary<int, int>();

            foreach (GraphNode node in nodes)
            {
                int k = 1;

                while (true)
                {
                    // Get all nodes of current K color
                    HashSet<GraphNode> nodesOfCurrentColor = colorsSets.ElementAt(k).Value != null ?
                            new HashSet<GraphNode>(colorsSets.ElementAt(k).Value) : new HashSet<GraphNode>();

                    // And try to find neighbours with this color
                    nodesOfCurrentColor.Intersect(node.GetNeighbours());

                    // if none - great, current K is suitable for coloring current node
                    if (!nodesOfCurrentColor.Any())
                    {
                        break;
                    }
                    // Otherwise  - continue cycle
                    k++;
                }

                if (k > maxColor)
                {
                    maxColor = k;
                    // New color, so create a new set for nodes
                    colorsSets.Add(k, new HashSet<GraphNode>());
                }
                colorsSets.ElementAt(k).Value.Add(node);
                colors.Add(node.GetIndex(), k);
            }

            return colors;
        }

        private void SortByColor(List<GraphNode> collection, Dictionary<int, int> colors)
        {
       //     collection.sort(Comparator.<GraphNode> comparingInt(left=>colors.get(left.getIndex())).reversed());
        }
    }
}