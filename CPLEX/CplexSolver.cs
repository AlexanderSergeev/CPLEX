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
        private INumVar[] numVars;
        private Dictionary<int, HashSet<GraphNode>> colorSets;
        private int bestResult;

        public CplexSolver(NewGraph graph)
        {
            this.graph = graph;
            colorSets = new Dictionary<int, HashSet<GraphNode>>();
            cplex = new Cplex();
            Initialize();
        }

        public int CallsCount { get; private set; }

        private void Initialize()
        {
            cplex.SetOut(null);

            // get heuristic solution
            var maxSets = GetMaxIndependentSets(new List<GraphNode>(graph.Nodes));
            colorSets = maxSets;
            bestResult = maxSets.Count;
            Console.WriteLine(bestResult);

            // initialize sets vars
            numVars = graph.Nodes.Select(node => cplex.NumVar(0, 1, $"x{node.Index}")).ToArray();

            // objective function -> min
            InitializeObjFunc(maxSets);

            // add constraints based on max independent sets
            AddIndependentSetsConstraints(maxSets);
        }

        private void InitializeObjFunc(Dictionary<int, HashSet<GraphNode>> independentSets)
        {
            // TODO sets objective
            var func = cplex.LinearNumExpr();
            foreach (var x in numVars)
                func.AddTerm(1, x);
            cplex.AddMinimize(func);
        }

        private void AddIndependentSetsConstraints(Dictionary<int, HashSet<GraphNode>> independentSets)
        {
            // TODO sets constraints
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


        public Dictionary<int, HashSet<GraphNode>> FindMaxColorSets()
        {
            FindMaxColorSetsInternal();
            cplex.End();
            return colorSets;
        }

        private void FindMaxColorSetsInternal()
        {
            CallsCount++;
            if (!cplex.Solve()) return;
            var objValue = cplex.GetObjValue();
            // TODO* if same value => decide if needs branching
            // TODO get dual values as weights & solve CGP heuristic
            var weights = numVars.Select(var => cplex.GetValue(var)).ToArray();
            var maxWeightedSets = GetMaxWeightIndependentSets(graph.Nodes, weights);
            // TODO if found set > 1 update objective and constraints
            AddIndependentSetsConstraints(maxWeightedSets);
            FindMaxColorSetsInternal();
            // TODO else solve CGP exact
            // TODO if found set > 1 update objective and constraints
            // TODO else понять нужно ли отбросить ветку
            var branchingVariable = numVars.FirstOrDefault(var => !cplex.GetValue(var).IsInteger());
            if (branchingVariable == null)
            {
                // TODO update bestResult
                return;
            }
            else
            {
                // TODO do branching 
                var constraint = cplex.AddGe(branchingVariable, 1);
                FindMaxColorSetsInternal();
                cplex.Remove(constraint);
                // TODO exclude branchingVariable set
                constraint = cplex.AddLe(branchingVariable, 0);
                FindMaxColorSetsInternal();
                cplex.Remove(constraint);
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

        private static Dictionary<int, HashSet<GraphNode>> GetMaxWeightIndependentSets(List<GraphNode> graphNodes,
            double[] weights)
        {
            var sets = new Dictionary<int, HashSet<GraphNode>>();
            var markedVertices = new List<GraphNode>();
            var key = 1;
            var sorted = new List<GraphNode>(graphNodes.OrderByDescending(node => weights[graphNodes.IndexOf(node)]));

            while (markedVertices.Count != graphNodes.Count)
            {
                var nodes = new List<GraphNode>(sorted);
                var set = new HashSet<GraphNode>();
                nodes.RemoveAll(x => markedVertices.Contains(x));

                while (nodes.Count != 0)
                {
                    var v = nodes.FirstOrDefault();
                    set.Add(v);
                    nodes.RemoveAll(x => x.Index == v.Index || v.Neighbours.Contains(x));
                }

                sets.Add(key, set);
                key++;
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