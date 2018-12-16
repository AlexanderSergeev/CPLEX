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
        private List<INumVar> numVars;
        private List<IRange> constraints;
        private Dictionary<int, HashSet<GraphNode>> currentColors;
        private Dictionary<int, HashSet<GraphNode>> excludedSets;
        private Dictionary<int, HashSet<GraphNode>> bestColorSets;
        private int bestResult;

        public CplexSolver(NewGraph graph)
        {
            this.graph = graph;
            bestColorSets = new Dictionary<int, HashSet<GraphNode>>();
            excludedSets = new Dictionary<int, HashSet<GraphNode>>();
            cplex = new Cplex();
            constraints = new List<IRange>();
            Initialize();
        }

        public int CallsCount { get; private set; }

        private void Initialize()
        {
            cplex.SetOut(null);

            // get heuristic solution
            var maxSets = GetMaxIndependentSets(new List<GraphNode>(graph.Nodes));
            currentColors = maxSets;
            bestColorSets = maxSets;
            bestResult = maxSets.Count;
            Console.WriteLine(bestResult);

            // initialize set vars
            numVars = maxSets.Select(set => cplex.NumVar(0, 1, $"x{set.Key}")).ToList();

            // objective function -> min
            InitializeObjFunc();

            // add constraints based on max independent sets
            InitializeConstraints(maxSets);
        }

        private void InitializeObjFunc()
        {
            var func = cplex.LinearNumExpr();
            foreach (var x in numVars)
                func.AddTerm(1, x);
            cplex.Remove(cplex.GetObjective());
            cplex.AddMinimize(func);
        }

        private void InitializeConstraints(Dictionary<int, HashSet<GraphNode>> independentSets)
        {
            cplex.Remove(constraints.ToArray());
            constraints = new List<IRange>();
            foreach (var node in graph.Nodes)
            {
                var numExpr = cplex.NumExpr();
                foreach (var independentSet in independentSets)
                {
                    if (independentSet.Value.Contains(node))
                    {
                        var curVar = numVars[independentSet.Key - 1];
                        numExpr = cplex.Sum(numExpr, curVar);
                    }
                }
                constraints.Add(cplex.AddGe(numExpr, 1));
            }
        }

        private void UpdateModel(Dictionary<int, HashSet<GraphNode>> independentSets)
        {
            foreach (var set in independentSets)
            {
                currentColors.Add(set.Key, set.Value);
                numVars.Add(cplex.NumVar(0, 1, $"x{set.Key}"));
            }
            InitializeObjFunc();
            InitializeConstraints(currentColors);
        }

        public Dictionary<int, HashSet<GraphNode>> FindMaxColorSets()
        {
            FindMaxColorSetsInternal();
            cplex.End();
            return bestColorSets;
        }

        private void FindMaxColorSetsInternal()
        {
            CallsCount++;
            if (!cplex.Solve()) return;
            var objValue = cplex.GetObjValue();
            // TODO* if same value => decide if needs branching
            var weights = constraints.Select(constraint => cplex.GetDual(constraint)).ToArray();
            var maxWeightedSets = GetMaxWeightIndependentSets(graph.Nodes, weights);
            if (maxWeightedSets.Count > 0)
            {
                UpdateModel(maxWeightedSets);
                FindMaxColorSetsInternal();
            }
            else
            {
                // TODO solve CGP exact
                // TODO if found update objective and constraints
                // TODO else понять нужно ли отбросить ветку
                var branchingVariable = numVars.FirstOrDefault(var => !cplex.GetValue(var).IsInteger());
                if (branchingVariable == null)
                {
                    if (objValue < bestResult)
                    {
                        var result = new Dictionary<int, HashSet<GraphNode>>();
                        var variables = numVars.Where(var => cplex.GetValue(var) == 1);
                        foreach (var variable in variables)
                        {
                            var key = int.Parse(variable.Name.Substring(1));
                            HashSet<GraphNode> set;
                            currentColors.TryGetValue(key, out set);
                            result.Add(key, set);
                        }
                        bestColorSets = result;
                        bestResult = result.Count;
                        Console.WriteLine(bestResult);
                    }
                    return;
                }
                else
                {
                    var branchingVariables = numVars.Where(var => !cplex.GetValue(var).IsInteger());
                    branchingVariable = branchingVariables.FirstOrDefault(var => cplex.GetValue(var) == branchingVariables.Max(x => cplex.GetValue(x)));
                    var constraint = cplex.AddGe(branchingVariable, 1);
                    constraints.Add(constraint);
                    FindMaxColorSetsInternal();
                    cplex.Remove(constraint);
                    constraints.Remove(constraint);

                    constraint = cplex.AddLe(branchingVariable, 0);
                    constraints.Add(constraint);
                    var excludedSet = currentColors.FirstOrDefault(set => "x" + set.Key.ToString() == branchingVariable.Name);
                    excludedSets.Add(excludedSet.Key, excludedSet.Value);
                    // TODO exclude branchingVariable set
                    FindMaxColorSetsInternal();
                    cplex.Remove(constraint);
                    constraints.Remove(constraint);
                    excludedSets.Remove(excludedSet.Key);
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
            return ExtendColorSets(sets);
        }

        private Dictionary<int, HashSet<GraphNode>> GetMaxWeightIndependentSets(List<GraphNode> graphNodes, double[] weights)
        {
            var sets = new Dictionary<int, HashSet<GraphNode>>();
            var markedVertices = new List<GraphNode>();
            var key = currentColors.Count + 1;
            var sorted = new List<GraphNode>(graphNodes.OrderByDescending(node => weights[graphNodes.IndexOf(node)]));

            while (markedVertices.Count != graphNodes.Count)
            {
                var setWeight = 0.0;
                var nodes = new List<GraphNode>(sorted);
                var set = new HashSet<GraphNode>();
                nodes.RemoveAll(x => markedVertices.Contains(x));

                while (nodes.Count != 0)
                {
                    var v = nodes.FirstOrDefault();
                    set.Add(v);
                    setWeight = setWeight + weights[graphNodes.IndexOf(v)];
                    nodes.RemoveAll(x => x.Index == v.Index || v.Neighbours.Contains(x));
                }
                if (setWeight > 1)
                {
                    sets.Add(key, set);
                    key++;
                }
                markedVertices.AddRange(set);
            }
            return ExtendColorSets(sets);
        }

        private static Dictionary<int, HashSet<GraphNode>> ExtendColorSets(Dictionary<int, HashSet<GraphNode>> colorSets)
        {
            foreach (var set in colorSets)
            {
                foreach (var previousSet in colorSets.Where(x => x.Key < set.Key))
                {
                    foreach (var node in previousSet.Value)
                    {
                        if (NoConnections(node, set.Value))
                        {
                            set.Value.Add(node);
                        }
                    }
                }

            }
            return colorSets;
        }

        private static bool NoConnections(GraphNode node, HashSet<GraphNode> set)
        {
            foreach (var setNode in set)
            {
                if (setNode.Neighbours.Contains(node))
                {
                    return false;
                }
            }
            return true;
        }
    }
}