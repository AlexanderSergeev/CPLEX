using System;
using System.Collections.Generic;
using System.Linq;

namespace CPLEX
{
    class Heuristic
    {
        private NewGraph graph;
        public List<GraphNode> maxClique;

        public Heuristic(NewGraph graph)
        {
            this.graph = graph;
            maxClique = new List<GraphNode>();
        }

        public List<GraphNode> FindMaxClique()
        {
            List<GraphNode> nodes = new List<GraphNode>(graph.GetNodes().Values);
            Dictionary<int, int> colors = FindChromaticNumber(nodes);
            // Sorting by color number - color number for node shows number of adjacent with each other vertices
            // and we are interested only in nodes with a lot of such neighbours.
            SortByColor(nodes, colors);
            int chromaticNumber = colors.Values.Distinct().Count();
            BranchAndBound(nodes, new List<GraphNode>(), colors);
            return maxClique;
        }

        public void BranchAndBound(List<GraphNode> candidates, List<GraphNode> clique, Dictionary<int, int> colors)
        {
            foreach (GraphNode c in candidates)
            {
                var colorValue = GetColorValue(c, colors);
                // we can predict if node cannot expand current max clique
                if (clique.Count + colorValue <= maxClique.Count)
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
                else if (currentClique.Count > maxClique.Count)
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
            nodes = nodes.OrderByDescending(o => o.GetNeighbours().Count).ToList();
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
                    HashSet<GraphNode> kColorNodes;
                    colorsSets.TryGetValue(k, out kColorNodes);
                    HashSet<GraphNode> nodesOfCurrentColor = kColorNodes != null ? kColorNodes : new HashSet<GraphNode>();
                    HashSet<GraphNode> neigboursOfCurrentColor = new HashSet<GraphNode>();

                    // And try to find neighbours with this color
                    foreach (GraphNode neigbour in node.GetNeighbours())
                    {
                        if (nodesOfCurrentColor.Contains(neigbour))
                        {
                            neigboursOfCurrentColor.Add(neigbour);
                        }
                    }

                    // if none - great, current K is suitable for coloring current node
                    if (neigboursOfCurrentColor.Count==0)
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
                HashSet<GraphNode> colorSetNodes = new HashSet<GraphNode>();
                colorsSets.TryGetValue(k, out colorSetNodes);
                colorSetNodes.Add(node);
                colorsSets.Remove(k);
                colorsSets.Add(k, colorSetNodes);
                colors.Add(node.GetIndex(), k);
            }

            return colors;
        }

        private int GetColorValue(GraphNode o, Dictionary<int, int> colors)
        {
            int colorValue;
            colors.TryGetValue(o.GetIndex(), out colorValue);
            return colorValue;
        }

        private void SortByColor(List<GraphNode> collection, Dictionary<int, int> colors)
        {
            collection = collection.OrderByDescending(o => GetColorValue(o, colors)).ToList();
        }
    }
}