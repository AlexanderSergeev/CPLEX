﻿using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly INumVar[] vars;

        public CplexSolver(NewGraph graph)
        {
            this.graph = graph;
            maxClique = new List<GraphNode>();
            cplex = new Cplex();
            vars = graph.Nodes.Select(node => cplex.NumVar(0, 1, $"x{node}")).ToArray();

            Initialize();
        }

        private void Initialize()
        {
            cplex.SetOut(null);

            // objective function -> max
            InitializeObjFunc();

            // add obvious constraints on nodes which are not connected
            AddPrimitiveConstraints();

            // add constraints based on independent sets
            //AddIndependentSetsConstraints();
        }

        private void AddIndependentSetsConstraints()
        {
            var independentSets = GetIndependentSets(new List<GraphNode>(graph.Nodes));
            var sets = independentSets.Values.Where(x => x.Count > 1);
            foreach (var set in sets)
            {
                var iloNumExpr = cplex.NumExpr();
                foreach (var node in set)
                {
                    var curVar = vars[node.Index - 1];
                    iloNumExpr = cplex.Sum(iloNumExpr, curVar);
                }

                cplex.AddLe(iloNumExpr, 1);
            }
        }

        private void AddPrimitiveConstraints()
        {
            foreach (var node in graph.Nodes)
                for (var anotherNodeIndex = node.Index + 1;
                    anotherNodeIndex <= graph.Nodes.Count;
                    anotherNodeIndex++)
                {
                    var anotherNode = graph[anotherNodeIndex];
                    if (!node.Neighbours.Contains(anotherNode))
                    {
                        var numVar = vars[node.Index - 1];
                        var anotherNumVar = vars[anotherNodeIndex - 1];
                        cplex.AddLe(cplex.Sum(numVar, anotherNumVar), 1);
                    }
                }
        }

        private void InitializeObjFunc()
        {
            var func = cplex.LinearNumExpr();
            foreach (var x in vars)
                func.AddTerm(1, x);
            cplex.AddMaximize(func);
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
            var objValue = cplex.GetObjValue();

            // this branch won't give us better result than existing one
            if (objValue < upperBound || objValue.Almost(upperBound))
            {
                RemoveAndGoNext();
            }
            var values = cplex.GetValues(vars);
            var firstFractalIndex = -1;
            var possibleMaxClique = new List<GraphNode>();
            for (var i = 0; i < values.Length; i++)
            {
                // if fractional var is found - doing branching basing on it
                if (!values[i].IsInteger())
                {
                    firstFractalIndex = i;
                    break;
                }
                // until we found fractal value of some var - it is potentially a clique
                if (values[i].Almost(1))
                {
                    possibleMaxClique.Add(graph.Nodes[i]);
                    using (var writer = new StreamWriter("log.txt", true))
                        writer.WriteLine(
                            $"Current clique: {string.Join(", ", possibleMaxClique.Select(x => x.Index))}");
                }
            }

            // it is an integer solution
            // if possible max clique is bigger then previous one - we found new max clique
            if (firstFractalIndex == -1)
            {
                if (possibleMaxClique.Count > upperBound)
                {
                    maxClique = possibleMaxClique;
                    upperBound = maxClique.Count;
                }
                RemoveAndGoNext();
            }
            else
            {
                // otherwise doing branching
                var newVar = vars[firstFractalIndex];
                var newBranchConstraint = cplex.AddGe(newVar, 1);
                FindCliqueInternal();
                cplex.Remove(newBranchConstraint);

                newBranchConstraint=cplex.AddLe(newVar, 0);
                FindCliqueInternal();
                cplex.Remove(newBranchConstraint);
            }
        }

        private static Dictionary<int, HashSet<GraphNode>> GetIndependentSets(IEnumerable<GraphNode> graphNodes)
        {
            // contains sets with vertexes of the same color. Key - color number, value - set of nodes of this color
            var colorsSets = new Dictionary<int, HashSet<GraphNode>>();
            var nodes = graphNodes.OrderByDescending(o => o.Neighbours.Count).ToList();

            foreach (var node in nodes)
            {
                var k = 1;

                while (true)
                {
                    // Get all nodes of current K color
                    HashSet<GraphNode> nodeSet;
                    colorsSets.TryGetValue(k, out nodeSet);

                    // And try to find neighbours with this color
                    if (!node.Neighbours.Any(x => nodeSet?.Contains(x) ?? false))
                        break;
                    // Otherwise  - continue cycle
                    k++;
                }
                if (!colorsSets.ContainsKey(k))
                    colorsSets[k] = new HashSet<GraphNode>();
                colorsSets[k].Add(node);
            }
            return colorsSets;
        }

        private void RemoveAndGoNext()
        {
            var firstOne = vars.FirstOrDefault(x => cplex.GetValue(x).Almost(1));
            if (firstOne == null) return;
            cplex.AddLe(firstOne, 0);
            FindCliqueInternal();
        }
    }
}