using System;
using System.Collections.Generic;
using System.Linq;
using ILOG.Concert;
using ILOG.CPLEX;

namespace CPLEX
{
    public class CplexSolver
    {
        private int upperBound;
        public int CallsCount { get; private set; }
        private readonly Cplex cplex;
        private readonly NewGraph graph;
        private List<GraphNode> maxClique;
        private readonly Dictionary<int, INumVar> vars;

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
            cplex.SetOut(Console.Out);

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
                var iloNumExpr = cplex.NumExpr();
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
                for (var anotherNodeIndex = node.GetIndex() + 1;
                    anotherNodeIndex <= graph.GetNodes().Count;
                    anotherNodeIndex++)
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

        private void InitializeObjFunc()
        {
            var func = cplex.LinearNumExpr();
            foreach (var x in vars.Values)
                func.AddTerm(1, x);
            cplex.AddMaximize(func);
        }

        private void InitializeVars()
        {
            foreach (var x in graph.GetNodes())
                vars.Add(x.Value.GetIndex(), cplex.NumVar(0, 1, x.Value.GetIndex() + "n"));
        }

        public List<GraphNode> FindMaxClique()
        {
            FindCliqueInternal();
            return maxClique;
        }

        private void FindCliqueInternal()
        {
            CallsCount++;
            if (!cplex.Solve()) return;
            // this branch won't give us better result than existing one
            var objValue = cplex.GetObjValue();
            if (upperBound > objValue || objValue.Almost(upperBound))
            {
                return;
            }
            var valuesArray = vars.Values.ToArray();
            var varsValues = cplex.GetValues(valuesArray);
            var firstFractalIndex = -1;
            var possibleMaxClique = new List<GraphNode>();
            for (var d = 0; d < varsValues.Length; d++)
            {
                // if fractional var is found - doing branching basing on it
                if (!varsValues[d].IsInteger())
                {
                    firstFractalIndex = d;
                    break;
                }

                // until we found fractal value of some var - it is potentially a clique
                if (varsValues[d].Almost(1))
                    if (maxClique.Count < possibleMaxClique.Count)
                    {
                        maxClique = possibleMaxClique;
                        upperBound = maxClique.Count;
                    }
                var nodes = graph.GetNodes();
                var node = nodes.ElementAt(d).Value;
                possibleMaxClique.Add(node);
            }

            // it is an integer solution
            // if possible max clique is bigger then previous one - we found new max clique
            if (firstFractalIndex == -1 && objValue.IsInteger())
            {
                if (maxClique.Count < possibleMaxClique.Count)
                {
                    // doing branching
                    INumVar newVar;
                    vars.TryGetValue(firstFractalIndex + 1, out newVar);
                    IRange newBranchConstraint = cplex.AddGe(newVar, 1);
                    FindCliqueInternal();
                    cplex.Remove(newBranchConstraint);

                    vars.TryGetValue(firstFractalIndex + 1, out newVar);
                    newBranchConstraint = cplex.AddLe(newVar, 0);
                    FindCliqueInternal();
                    cplex.Remove(newBranchConstraint);

                    maxClique = possibleMaxClique;
                    upperBound = maxClique.Count;
                }
            }
            else
            {
                // otherwise doing branching
                INumVar newVar;
                vars.TryGetValue(firstFractalIndex + 1, out newVar);
                var newBranchConstraint = cplex.AddGe(newVar, 1);
                FindCliqueInternal();
                cplex.Remove(newBranchConstraint);

                vars.TryGetValue(firstFractalIndex + 1, out newVar);
                cplex.AddLe(newVar, 0);
                FindCliqueInternal();
            }
        }

        public List<GraphNode> GetMaxClique()
        {
            return maxClique;
        }

        private static Dictionary<int, HashSet<GraphNode>> GetIndependentSets(List<GraphNode> graphNodes)
        {
            var maxColor = 0;
            // contains sets with vertexes of the same color. Key - color number, value - set of nodes of this color
            var colorsSets = new Dictionary<int, HashSet<GraphNode>>();
            var colors = new Dictionary<int, int>();
            var nodes = graphNodes.OrderByDescending(o => o.GetNeighbours().Count).ToList();

            foreach (var node in nodes)
            {
                var k = 1;

                while (true)
                {
                    // Get all nodes of current K color
                    HashSet<GraphNode> nodeSet;
                    if (!colorsSets.TryGetValue(k, out nodeSet))
                        nodeSet = new HashSet<GraphNode>();

                    // And try to find neighbours with this color
                    var nodesOfCurrentColor = new HashSet<GraphNode>();
                    var neighbours = node.GetNeighbours();
                    foreach (var neighbour in neighbours)
                    {
                        if (nodeSet.Contains(neighbour))
                            nodesOfCurrentColor.Add(neighbour);
                    }

                    // if none - great, current K is suitable for coloring current node
                    if (nodesOfCurrentColor.Count == 0)
                        break;
                    // Otherwise  - continue cycle
                    k++;
                }

                if (k > maxColor)
                {
                    maxColor = k;
                    // New color, so create a new set for nodes
                    colorsSets.Add(k, new HashSet<GraphNode>());
                }
                HashSet<GraphNode> kNodeList;
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