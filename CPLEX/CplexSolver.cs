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
                    var curVar = numVars[graph.Nodes.IndexOf(node)];
                    numExpr = cplex.Sum(numExpr, curVar);
                }

                cplex.AddLe(numExpr, 1);
            }
        }

        private void AddIndependentSetsConstraints(IEnumerable<GraphNode> set)
        {
            var numExpr = cplex.NumExpr();
            foreach (var node in set)
            {
                var curVar = numVars[graph.Nodes.IndexOf(node)];
                numExpr = cplex.Sum(numExpr, curVar);
            }

            cplex.AddLe(numExpr, 1);
        }

        private void AddEdgesConstraints(IEnumerable<Tuple<GraphNode, GraphNode>> edges)
        {
            foreach (var edge in edges)
            {
                var numExpr = cplex.NumExpr();
                var curVar = numVars[graph.Nodes.IndexOf(edge.Item1)];
                numExpr = cplex.Sum(numExpr, curVar);
                curVar = numVars[graph.Nodes.IndexOf(edge.Item2)];
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
            var graphNodes = new List<GraphNode>(graph.Nodes.OrderByDescending(o => o.Neighbours.Count));
            var clique = new List<GraphNode>
            {
                graphNodes[0]
            };
            graphNodes.Remove(graphNodes[0]);

            foreach (var node in graphNodes)
            {
                if (IsNodeConnectedToAllNeighbours(node, clique))
                {
                    clique.Add(node);
                }
            }
            maxClique = clique;
            bestResult = clique.Count;
        }

        private static bool IsClique(List<GraphNode> possibleClique)
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

        private static bool IsNodeConnectedToAllNeighbours(GraphNode node, IEnumerable<GraphNode> neighbours)
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
            if (objValue < bestResult + 1)
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
                if (objValue.Almost(previousObjValue, 0.01))
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
            var maxWeightedSet = new List<GraphNode>();
            var maxWeight = 0.0;
            var nodes = new List<GraphNode>(graphNodes.OrderByDescending(node => weights[graphNodes.IndexOf(node)]));

            for (var i = 0; i < nodes.Count; i++)
            {
                var set = new List<GraphNode>();
                var removedVertices = new List<GraphNode>();
                var setWeight = 0.0;

                for (var j = i; j < nodes.Count; j++)
                {
                    var node = nodes[j];
                    if (!removedVertices.Contains(node))
                    {
                        set.Add(node);
                        removedVertices.Add(node);
                        removedVertices.AddRange(node.Neighbours);
                        setWeight = setWeight + weights[graphNodes.IndexOf(node)];
                    }
                }
                if (setWeight > maxWeight || (setWeight.Almost(maxWeight) && set.Count > maxWeightedSet.Count))
                {
                    maxWeightedSet = set;
                    maxWeight = setWeight;
                }
            }
            /*
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
            }*/
            return maxWeightedSet;
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