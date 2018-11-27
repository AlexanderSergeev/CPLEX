using System.Collections.Generic;
using System.Linq;
using ILOG.Concert;
using ILOG.CPLEX;
using System;

namespace CPLEX
{
    public class CplexSolver
    {
        private readonly Cplex cplex;
        private readonly NewGraph graph;
        private readonly INumVar[] numVars;
        private List<GraphNode> maxClique;
        private int bestResult;
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

            // add constraints based on max independent sets
            AddIndependentSetsConstraints(GetMaxIndependentSets(new List<GraphNode>(graph.Nodes)));
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
            var numExpr = cplex.NumExpr();
            foreach (var node in set)
            {
                var curVar = numVars[node.Index - 1];
                numExpr = cplex.Sum(numExpr, curVar);
            }

            cplex.AddLe(numExpr, 1);
        }

        private void AddEdgesConstraints(List<Tuple<GraphNode, GraphNode>> edges)
        {
            foreach (var edge in edges)
            {
                var numExpr = cplex.NumExpr();
                var curVar = numVars[edge.Item1.Index - 1];
                numExpr = cplex.Sum(numExpr, curVar);
                curVar = numVars[edge.Item2.Index - 1];
                numExpr = cplex.Sum(numExpr, curVar);

                cplex.AddLe(numExpr, 1);
            }
        }
        

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
            bestResult = clique.Count;
        }

        private bool IsClique(List<GraphNode> possibleClique)
        {
            foreach (var node in possibleClique)
            {
                if (!IsNodeConnectedToAllNeighbours(node, possibleClique))
                {
                    return false;
                }
            }
            return true;
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
            if (objValue < bestResult || objValue.Almost(bestResult))
                return;
            var branchingVariable = numVars.FirstOrDefault(var => !cplex.GetValue(var).IsInteger());
            // решение целое
            if (branchingVariable == null)
            {
                var values = cplex.GetValues(numVars);
                var possibleClique = graph.Nodes.Where((_, i) => values[i].Almost(1)).ToList();
                // найденное решение - клика
                if (IsClique(possibleClique))
                {
                    maxClique = possibleClique;
                    bestResult = maxClique.Count;
                    return;
                }
                // найденное решение - не клика
                var disconnectedEdges = GetCliqueDisconnectedEdges(possibleClique);
                AddEdgesConstraints(disconnectedEdges);
                previousObjValue = objValue;
                FindCliqueInternal();
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
                    var maxWeightedSet = GetMaxWeightIndependentSet(graph.Nodes, weights);
                    AddIndependentSetsConstraints(maxWeightedSet);
                    previousObjValue = objValue;
                    FindCliqueInternal();
                }
            }
        }

        /*private static Dictionary<int, HashSet<GraphNode>> GetIndependentSets(IEnumerable<GraphNode> graphNodes)
        {
            // contains sets with vertices of the same color. Key - color number, value - set of nodes of this color
            var colorsSets = new Dictionary<int, HashSet<GraphNode>>();
            var nodes = graphNodes.OrderByDescending(o => o.Neighbours.Count);

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
        }*/

        private static Dictionary<int, HashSet<GraphNode>> GetMaxIndependentSets(List<GraphNode> graphNodes)
        {
            var sets = new Dictionary<int, HashSet<GraphNode>>();
            var markedVertices = new List<GraphNode>();
            var key = 1;

            while (markedVertices.Count != graphNodes.Count)
            {
                var nodes = new List<GraphNode>(graphNodes);
                var set = new HashSet<GraphNode>();
                nodes.RemoveAll(x => markedVertices.Contains(x));

                while (nodes.Count != 0)
                {
                    var sorted = nodes.OrderBy(o => o.Neighbours.Count(x => nodes.Contains(x)));
                    var v = sorted.FirstOrDefault();
                    set.Add(v);
                    nodes.RemoveAll(x => x.Index == v.Index || v.Neighbours.Contains(x));
                }
                sets.Add(key, set);
                key++;
                markedVertices.AddRange(set);
            }
            return sets;
        }

        private static List<GraphNode> GetMaxWeightIndependentSet(List<GraphNode> graphNodes, double[] weights)
        {
            var maxWeightSet = new List<GraphNode>();
            var maxWeight = 0.0;
            var markedVertices = new List<GraphNode>();

            while (markedVertices.Count != graphNodes.Count)
            {
                var nodes = new List<GraphNode>(graphNodes);
                var set = new List<GraphNode>();
                var setWeight = 0.0;
                nodes.RemoveAll(x => markedVertices.Contains(x));

                while (nodes.Count != 0)
                {
                    var sorted = nodes.OrderByDescending(node => weights[graphNodes.IndexOf(node)]);
                    var v = sorted.FirstOrDefault();
                    set.Add(v);
                    setWeight = setWeight + weights[graphNodes.IndexOf(v)];
                    nodes.RemoveAll(x => x.Index == v.Index || v.Neighbours.Contains(x));
                }
                if (setWeight > maxWeight || (setWeight.Almost(maxWeight) && set.Count>maxWeightSet.Count))
                {
                    maxWeight = setWeight;
                    maxWeightSet = set;
                }
                markedVertices.AddRange(set);
            }
            return maxWeightSet;
        }

        private static List<Tuple<GraphNode, GraphNode>> GetCliqueDisconnectedEdges(List<GraphNode> clique)
        {
            var disconnectedEdges = new List<Tuple<GraphNode, GraphNode>>();

            for (int i=0;i<clique.Count;i++)
            {
                for (int j=i+1;j<clique.Count;j++)
                {
                    if (!clique[i].Neighbours.Contains(clique[j]))
                    {
                        disconnectedEdges.Add(new Tuple<GraphNode, GraphNode>(clique[i], clique[j]));
                    }
                }
            }
            return disconnectedEdges;
        }
    }
}