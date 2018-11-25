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

            // add constraints based on max independent sets (greedy graph coloring)
            AddMaxIndependentSetsConstraints();
        }

        private void AddMaxIndependentSetsConstraints()
        {
            var independentSets = GetMaxIndependentSets(new List<GraphNode>(graph.Nodes));
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
            FindCliqueInternal();
            cplex.End();
            return maxClique;
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
                if (IsClique(values))
                {
                    maxClique = graph.Nodes.Where((_, i) => values[i].Almost(1)).ToList();
                    upperBound = maxClique.Count;
                    return;
                }
                else
                {
                    // TODO: добавить ограничение
                    // var constraint = cplex.AddGe(branchingVariable, 1);
                    return;
                }
            }
            // решение дробное
            // TODO: SP + ветвление
            var constraint = cplex.AddGe(branchingVariable, 1);
            FindCliqueInternal();
            cplex.Remove(constraint);

            constraint = cplex.AddLe(branchingVariable, 0);
            FindCliqueInternal();
            cplex.Remove(constraint);
        }

        private bool IsClique(double[] values)
        {
            var possibleClique = graph.Nodes.Where((_, i) => values[i].Almost(1)).ToList();
            foreach (var node in possibleClique)
            {
                foreach (var neighbour in possibleClique)
                {
                    if (node.Index!=neighbour.Index &&!node.Neighbours.Contains(neighbour))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static Dictionary<int, HashSet<GraphNode>> GetMaxIndependentSets(IEnumerable<GraphNode> graphNodes)
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
            // Get max length sets
            var maxLength = 0;
            var maxColorsSets = new Dictionary<int, HashSet<GraphNode>>();
            foreach (var colorSet in colorsSets)
            {
                if (colorSet.Value.Count > maxLength)
                {
                    maxColorsSets = new Dictionary<int, HashSet<GraphNode>>();
                    maxColorsSets.Add(colorSet.Key, colorSet.Value);
                    maxLength = colorSet.Value.Count;
                }
                else if (colorSet.Value.Count == maxLength)
                {
                    maxColorsSets.Add(colorSet.Key, colorSet.Value);
                }
            }
            return maxColorsSets;
        }
    }
}