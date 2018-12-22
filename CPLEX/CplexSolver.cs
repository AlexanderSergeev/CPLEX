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
        private List<HashSet<GraphNode>> excludedSets;
        private Dictionary<int, HashSet<GraphNode>> bestColorSets;
        private int bestResult;
        private double previousObjValue;

        public CplexSolver(NewGraph graph)
        {
            this.graph = graph;
            bestColorSets = new Dictionary<int, HashSet<GraphNode>>();
            excludedSets = new List<HashSet<GraphNode>>();
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
            bestColorSets = new Dictionary<int, HashSet<GraphNode>>(maxSets);
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
                        var curVar = numVars[independentSets.ToList().IndexOf(independentSet)];
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
            var weights = constraints.Select(constraint => cplex.GetDual(constraint)).ToArray();
            var maxWeightedSets = GetMaxWeightIndependentSets(graph.Nodes, weights);
            if (maxWeightedSets.Count > 0)
            {
                UpdateModel(maxWeightedSets);
                previousObjValue = objValue;
                FindMaxColorSetsInternal();
            }
            else
            {
                var branchingVariable = numVars.FirstOrDefault(var => !cplex.GetValue(var).IsInteger());
                if (objValue < bestResult && !objValue.Almost(bestResult) && !previousObjValue.Almost(objValue) && branchingVariable != null)
                {
                    Branch(objValue);
                }
                else
                {
                    // solve CGP exact
                    var exactMaxWeightedSet = SolveCGPWithCplex(graph.Nodes, weights);
                    if (exactMaxWeightedSet != null)
                    {
                        var sets = new Dictionary<int, HashSet<GraphNode>>();
                        var key = currentColors.Last().Key + 1;
                        sets.Add(key, exactMaxWeightedSet);
                        UpdateModel(sets);
                        previousObjValue = objValue;
                        FindMaxColorSetsInternal();
                    }
                    else
                    {
                        if (objValue > bestResult || objValue.Almost(bestResult))
                        {
                            return;
                        }

                        if (branchingVariable == null)
                        {
                            var result = new Dictionary<int, HashSet<GraphNode>>();
                            var variables = numVars.Where(var => cplex.GetValue(var).Almost(1));
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
                        else if (!previousObjValue.Almost(objValue))
                        {
                            Branch(objValue);
                        }
                    }
                }
            }
        }

        private void Branch(double objValue)
        {
            var branchingVariables = numVars.Where(var => !cplex.GetValue(var).IsInteger());
            var branchingVariable = branchingVariables.FirstOrDefault(var =>
                cplex.GetValue(var) == branchingVariables.Max(x => cplex.GetValue(x)));
            var constraint = cplex.AddEq(branchingVariable, 1);
            constraints.Add(constraint);
            previousObjValue = objValue;
            FindMaxColorSetsInternal();
            cplex.Remove(constraint);
            constraints.Remove(constraint);

            constraint = cplex.AddEq(branchingVariable, 0);
            constraints.Add(constraint);
            var excludedSet =
                currentColors.FirstOrDefault(set => "x" + set.Key.ToString() == branchingVariable.Name);
            excludedSets.Add(excludedSet.Value);
            previousObjValue = objValue;
            FindMaxColorSetsInternal();
            cplex.Remove(constraint);
            constraints.Remove(constraint);
            excludedSets.Remove(excludedSet.Value);
        }

        private Dictionary<int, HashSet<GraphNode>> GetMaxIndependentSets(List<GraphNode> graphNodes)
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

            foreach (var set in sets)
            {
                foreach (var previousSet in sets.Where(x => x.Key < set.Key))
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
            return sets;
        }

        private Dictionary<int, HashSet<GraphNode>> GetMaxWeightIndependentSets(List<GraphNode> graphNodes, double[] weights)
        {
            var sets = new Dictionary<int, HashSet<GraphNode>>();
            var markedVertices = new List<GraphNode>();
            var key = currentColors.Last().Key + 1;
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
                if (setWeight > 1 && !setWeight.Almost(1))
                {
                    sets.Add(key, set);
                    key++;
                }
                markedVertices.AddRange(set);
            }
            return ExtendColorSets(sets);
        }

        private Dictionary<int, HashSet<GraphNode>> ExtendColorSets(Dictionary<int, HashSet<GraphNode>> colorSets)
        {
            var resultSets = new Dictionary<int, HashSet<GraphNode>>();
            foreach (var set in colorSets)
            {
                var extendedSet = ExtendColorSet(set.Value);
                if (extendedSet != null)
                {
                    foreach (var previousSet in colorSets.Where(x => x.Key < set.Key))
                    {
                        foreach (var node in previousSet.Value)
                        {
                            if (NoConnections(node, extendedSet))
                            {
                                extendedSet.Add(node);
                            }
                        }
                    }
                    resultSets.Add(set.Key, extendedSet);
                    foreach (var excludedSet in excludedSets)
                    {
                        if (extendedSet.SetEquals(excludedSet))
                        {
                            resultSets.Remove(set.Key);
                        }
                    }
                }
            }
            return resultSets;
        }

        private HashSet<GraphNode> ExtendColorSet(HashSet<GraphNode> colorSet, bool exclude = true)
        {
            foreach (var set in currentColors)
            {
                foreach (var node in set.Value)
                {
                    if (NoConnections(node, colorSet))
                    {
                        colorSet.Add(node);
                    }
                }
            }

            if (exclude)
            {
                foreach (var excludedSet in excludedSets)
                {
                    if (colorSet.SetEquals(excludedSet))
                    {
                        return null;
                    }
                }
            }

            return colorSet;
        }

        private static bool NoConnections(GraphNode node, HashSet<GraphNode> set)
        {
            foreach (var setNode in set)
            {
                if (setNode.Neighbours.Contains(node) || setNode.Index == node.Index)
                {
                    return false;
                }
            }
            return true;
        }

        private HashSet<GraphNode> SolveCGPWithCplex(List<GraphNode> graphNodes, double[] weights)
        {
            var cgpCplex = new Cplex();
            var vars = new List<IIntVar>();
            var func = cgpCplex.LinearNumExpr();
            foreach (var node in graphNodes)
            {
                var newVar = cgpCplex.BoolVar($"n{node}");
                vars.Add(newVar);
                func.AddTerm(weights[graph.Nodes.IndexOf(node)], newVar);
            }
            cgpCplex.AddMaximize(func);

            foreach (var node in graphNodes)
            {
                foreach (var neighbour in node.Neighbours)
                {
                    var indexVar = vars[graphNodes.IndexOf(node)];
                    var neighbourIndexVar = vars[graphNodes.IndexOf(neighbour)];
                    cgpCplex.AddLe(cplex.Sum(indexVar, neighbourIndexVar), 1);
                }
            }
            foreach (var excludedSet in excludedSets)
            {
                var numExpr = cplex.NumExpr();
                foreach (var node in excludedSet)
                {
                    var curVar = vars[graphNodes.IndexOf(node)];
                    numExpr = cplex.Sum(numExpr, curVar);
                }
                cgpCplex.AddLe(numExpr, excludedSet.Count - 1);
            }

            cgpCplex.SetOut(null);
            cgpCplex.Solve();
            var objValue = cgpCplex.GetObjValue();
            if (objValue > 1 && !objValue.Almost(1))
            {
                var result = new HashSet<GraphNode>();
                var variables = vars.Where(var => cgpCplex.GetValue(var).Almost(1));
                foreach (var variable in variables)
                {
                    result.Add(graphNodes.FirstOrDefault(x => x.Index == int.Parse(variable.Name.Substring(1))));
                }
                cgpCplex.End();
                return ExtendColorSet(result, false);
            }
            cgpCplex.End();
            return null;
        }
    }
}