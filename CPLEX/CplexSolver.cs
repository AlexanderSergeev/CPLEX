using System.Collections.Generic;
using System.Linq;
using ILOG.Concert;
using ILOG.CPLEX;

namespace CPLEX
{
    public class CplexSolver
    {
        private readonly Cplex cplex;
        private readonly NewGraph graph;
        private readonly INumVar[] numVars;
        private List<GraphNode> maxClique;
        private int upperBound;
        private double previousObjValue;

        public CplexSolver(NewGraph graph)
        {
            this.graph = graph;
            maxClique = new List<GraphNode>();
            cplex = new Cplex();
            numVars = graph.Nodes.Select(node => cplex.NumVar(0, 1, $"x{node.Index}")).ToArray();

            Initialize();
        }

        public int CallsCount { get; private set; }

        private void Initialize()
        {
            cplex.SetOut(null);

            // objective function -> max
            InitializeObjFunc();

            // add constraints on nodes which are not connected
            //AddPrimitiveConstraints();

            // add constraints based on independent sets (greedy graph coloring)
            AddIndependentSetsConstraints(GetIndependentSets(new List<GraphNode>(graph.Nodes), false));
        }

        private void AddIndependentSetsConstraints(Dictionary<int, HashSet<GraphNode>> independentSets)
        {
            var sets = independentSets.Values.Where(x => x.Count > 1);
            foreach (var set in sets)
            {
                var numExpr = cplex.NumExpr();
                foreach (var node in set)
                {
                    var curVar = numVars[node.Index - 1];
                    numExpr = cplex.Sum(numExpr, curVar);
                }

                cplex.AddLe(numExpr, 1);
            }
        }

        private void AddIndependentSetsConstraints(List<GraphNode> set)
        {
            if (set.Count > 1)
            {
                var numExpr = cplex.NumExpr();
                foreach (var node in set)
                {
                    var curVar = numVars[node.Index - 1];
                    numExpr = cplex.Sum(numExpr, curVar);
                }

                cplex.AddLe(numExpr, 1);
            }
        }

        /*private void AddPrimitiveConstraints()
        {
            foreach (var node in graph.Nodes)
                for (var anotherNodeIndex = node.Index + 1;
                    anotherNodeIndex <= graph.Nodes.Count;
                    anotherNodeIndex++)
                {
                    var anotherNode = graph[anotherNodeIndex];
                    if (!node.Neighbours.Contains(anotherNode))
                    {
                        var numVar = numVars[node.Index - 1];
                        var anotherNumVar = numVars[anotherNodeIndex - 1];
                        cplex.AddLe(cplex.Sum(numVar, anotherNumVar), 1);
                    }
                }
        }*/

        private void InitializeObjFunc()
        {
            var func = cplex.LinearNumExpr();
            foreach (var x in numVars)
                func.AddTerm(1, x);
            cplex.AddMaximize(func);
        }

        public List<GraphNode> FindMaxClique()
        {
            FindMaxCliqueWithHeuristics();
            FindCliqueInternal();
            cplex.End();
            return maxClique;
        }

        private void FindMaxCliqueWithHeuristics()
        {
            var graphNodes = new List<GraphNode>(graph.Nodes.OrderBy(o => o.Neighbours.Count));
            var clique = new List<GraphNode>(graphNodes);

            foreach (var node in graphNodes)
            {
                if (!IsNodeConnectedToAllNeighbours(node, clique))
                {
                    clique.Remove(node);
                }
            }
            maxClique = clique;
            upperBound = clique.Count;
        }

        private bool IsNodeConnectedToAllNeighbours(GraphNode node, List<GraphNode> neighbours)
        {
            foreach (var neighbour in neighbours)
            {
                if (node.Index != neighbour.Index && !node.Neighbours.Contains(neighbour))
                {
                    return false;
                }
            }
            return true;
        }

        private void FindCliqueInternal()
        {
            CallsCount++;
            if (!cplex.Solve()) return;
            var objValue = cplex.GetObjValue();
            // this branch won't give us better result than existing one
            if (objValue < upperBound || objValue.Almost(upperBound))
                return;
            var branchingVariable = numVars.FirstOrDefault(var => !cplex.GetValue(var).IsInteger());
            // решение целое
            if (branchingVariable == null)
            {
                var values = cplex.GetValues(numVars);
                var possibleClique = graph.Nodes.Where((_, i) => values[i].Almost(1)).ToList();
                var independentSets = GetIndependentSets(possibleClique, true);
                // найденное решение - клика
                if (possibleClique.Count == independentSets.Count)
                {
                    maxClique = possibleClique;
                    upperBound = maxClique.Count;
                    return;
                }
                // найденное решение - не клика
                else
                {
                    AddIndependentSetsConstraints(independentSets);
                    previousObjValue = objValue;
                    FindCliqueInternal();
                }
            }
            // решение дробное
            else
            {
                // ветвление, если f тоже самое
                if (objValue.Almost(previousObjValue))
                {
                    var constraint = cplex.AddGe(branchingVariable, 1);
                    previousObjValue = objValue;
                    FindCliqueInternal();
                    cplex.Remove(constraint);

                    constraint = cplex.AddLe(branchingVariable, 0);
                    previousObjValue = objValue;
                    FindCliqueInternal();
                    cplex.Remove(constraint);
                }
                // SP
                else
                {
                    var weights = numVars.Select(var => cplex.GetValue(var)).ToArray();
                    var graphNodes = new List<GraphNode>(graph.Nodes.OrderBy(node => weights[graph.Nodes.IndexOf(node)]));
                    var maxWeightedSet = new List<GraphNode>();
                    var maxWeight = 0.0;

                    for (int i = 0; i < graphNodes.Count; i++)
                    {
                        var set = new List<GraphNode>();
                        var removedVertices = new List<GraphNode>();
                        var setWeight = 0.0;
                        for (int j = i; j < graphNodes.Count; j++)
                        {
                            var node = graphNodes[j];
                            if (!removedVertices.Contains(node))
                            {
                                set.Add(node);
                                removedVertices.Add(node);
                                removedVertices.AddRange(node.Neighbours);
                                setWeight = setWeight + cplex.GetValue(numVars[j]);
                            }
                        }
                        if (maxWeight<setWeight)
                        {
                            maxWeightedSet = set;
                            maxWeight = setWeight;
                        }
                    }
                    AddIndependentSetsConstraints(maxWeightedSet);
                    previousObjValue = objValue;
                    FindCliqueInternal();
                }
            }
        }

        private static Dictionary<int, HashSet<GraphNode>> GetIndependentSets(IEnumerable<GraphNode> graphNodes, bool isClique)
        {
            // contains sets with vertices of the same color. Key - color number, value - set of nodes of this color
            var colorsSets = new Dictionary<int, HashSet<GraphNode>>();
            IEnumerable<GraphNode> nodes = null;
            if (!isClique)
            {
               nodes = graphNodes.OrderByDescending(o => o.Neighbours.Count);
            }
            else
            // для клики считаем соседей только между узлами клики
            {
                nodes = graphNodes.OrderByDescending(o => o.Neighbours.Count(n=>graphNodes.Contains(n)));
            }


            foreach (var node in nodes)
            {
                var k = 1;

                while (true)
                {
                    // Get all nodes of current K color
                    HashSet<GraphNode> nodeSet;
                    colorsSets.TryGetValue(k, out nodeSet);

                    // And try to find neighbours with this color
                    if (!(nodeSet?.Intersect(node.Neighbours).Any() ?? false))
                        break;
                    // Otherwise  - continue cycle
                    k++;
                }
                if (!colorsSets.ContainsKey(k))
                    colorsSets[k] = new HashSet<GraphNode>();
                colorsSets[k].Add(node);
            }
           return colorsSets;
            return colorsSets;
        }
    }
}