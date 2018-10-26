using System;
using System.Linq;
using ILOG.Concert;
using ILOG.CPLEX;
using System.Collections.Generic;

namespace CPLEX
{
    public class CplexSolver
    {
        private NewGraph graph;
        private List<GraphNode> maxClique;
        private Dictionary<int, INumVar> vars;
        private Cplex cplex;

        public CplexSolver(NewGraph graph)
        {
            this.graph = graph;
            maxClique = new List<GraphNode>();
            cplex = new Cplex();
            vars = new Dictionary<int, INumVar>();

            Initialize();
        }

        private void Initialize()
        {
            cplex.SetOut(null);

            // Variables
            InitializeVars();

            // objective function -> max
            InitializeObjFunc();

            // add obvious constraints on nodes which are not connected
            AddPrimitiveConstraints();

            // add constraints based on independent sets
            AddIndependentSetsConstraints();
        }

        private void AddIndependentSetsConstraints()
        {
            var independentSets = GetIndependentSets(new List<GraphNode>(graph.GetNodes().Values));
            var sets = independentSets.Values.Where(x => x.Count > 1);
            foreach (var set in sets)
            {
                INumExpr iloNumExpr = cplex.NumExpr();
                foreach (var node in set)
                {
                    INumVar curVar;
                    vars.TryGetValue(node.GetIndex(), out curVar);
                    iloNumExpr = cplex.Sum(iloNumExpr, curVar);
                }

                cplex.AddLe(iloNumExpr, 1);
            }
        }

        private void AddPrimitiveConstraints()
        {
            foreach (var node in graph.GetNodes().Values)
            {
                for (int anotherNodeIndex = node.GetIndex() + 1; anotherNodeIndex <= graph.GetNodes().Count; anotherNodeIndex++)
                {
                    GraphNode anotherNodeValue;
                    graph.GetNodes().TryGetValue(anotherNodeIndex, out anotherNodeValue);
                    if (!node.GetNeighbours().Contains(anotherNodeValue))
                    {
                        INumVar indexVar;
                        INumVar anotherNodeIndexVar;
                        vars.TryGetValue(node.GetIndex(), out indexVar);
                        vars.TryGetValue(anotherNodeIndex, out anotherNodeIndexVar);
                        cplex.AddLe(cplex.Sum(indexVar, anotherNodeIndexVar), 1);
                    }
                }
            }
        }

        private void InitializeObjFunc()
        {
            ILinearNumExpr func = cplex.LinearNumExpr();
            foreach (var x in vars.Values)
            {
                func.AddTerm(1, x);
            }
            cplex.AddMaximize(func);
        }

        private void InitializeVars()
        {
            foreach (var x in graph.GetNodes())
            {
                vars.Add(x.Value.GetIndex(), cplex.NumVar(0, 1, x.Value.GetIndex().ToString() + "n"));
            }
        }

        public List<GraphNode> FindMaxClique()
        {
            FindCliqueInternal();
            return maxClique;
        }

        private void FindCliqueInternal()
        {
            if (cplex.Solve())
            {
                // this branch won't give us better result than existing one
                var objValue = Math.Floor(cplex.GetObjValue());
                if (maxClique.Count > objValue)
                {
                    return;
                }
                var valuesArray = new INumVar[vars.Count];
                vars.Values.CopyTo(valuesArray, 0);
                double[] varsValues = cplex.GetValues(valuesArray);
                int firstFractalIndex = -1;
                List<GraphNode> possibleMaxClique = new List<GraphNode>();
                for (int d = 0; d < varsValues.Length; d++)
                {
                    // if fractional var is found - doing branching basing on it
                    if (varsValues[d] % 1 != 0.0)
                    {
                        firstFractalIndex = d;
                        break;
                    }

                    // until we found fractal value of some var - it is potentially a clique
                    if (varsValues[d] == 1.0)
                    {
                        GraphNode node;
                        var nodes = graph.GetNodes();
                        node = nodes.ElementAt(d).Value;
                        possibleMaxClique.Add(node);
                    }
                }
                // it is an integer solution
                // if possible max clique is bigger then previous one - we found new max clique
                if (firstFractalIndex == -1)
                {
                    if (maxClique.Count < possibleMaxClique.Count)
                    {
                        maxClique = possibleMaxClique;
                    }
                }
                else
                {
                    // otherwise doing branching
                    INumVar newVar;
                    vars.TryGetValue(firstFractalIndex + 1, out newVar);
                    IRange newBranchConstraint = cplex.AddGe(newVar, 1);
                    FindCliqueInternal();
                    cplex.Remove(newBranchConstraint);

                    vars.TryGetValue(firstFractalIndex + 1, out newVar);
                    newBranchConstraint = cplex.AddLe(newVar, 0);
                    FindCliqueInternal();
                    cplex.Remove(newBranchConstraint);
                }
            }
        }

        public List<GraphNode> GetMaxClique()
        {
            return maxClique;
        }

        private static Dictionary<int, List<GraphNode>> GetIndependentSets(List<GraphNode> nodes)
        {
            int maxColor = 0;
            // contains sets with vertexes of the same color. Key - color number, value - set of nodes of this color
            Dictionary<int, List<GraphNode>> colorsSets = new Dictionary<int, List<GraphNode>>();
            Dictionary<int, int> colors = new Dictionary<int, int>();

            foreach (var node in nodes)
            {
                int k = 1;

                while (true)
                {
                    // Get all nodes of current K color
                    List<GraphNode> nodeSet;
                    if (!colorsSets.TryGetValue(k, out nodeSet))
                    {
                        nodeSet = new List<GraphNode>();
                    }

                    // And try to find neighbours with this color
                    var nodesOfCurrentColor = new List<GraphNode>();
                    var neighbours = node.GetNeighbours();
                    foreach (var neighbour in neighbours)
                    {
                        if (nodeSet.Contains(neighbour))
                        {
                            nodesOfCurrentColor.Add(neighbour);
                        }
                    }

                    // if none - great, current K is suitable for coloring current node
                    if (nodesOfCurrentColor.Count == 0)
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
                    colorsSets.Add(k, new List<GraphNode>());
                }
                List<GraphNode> kNodeList;
                colorsSets.TryGetValue(k, out kNodeList);
                kNodeList.Add(node);
                colorsSets.Remove(k);
                colorsSets.Add(k, kNodeList);
                colors.Add(node.GetIndex(), k);
            }

            return colorsSets;
        }
    }
}