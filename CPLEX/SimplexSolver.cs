using System;
using ILOG.Concert;
using ILOG.CPLEX;

namespace MaxClique
{
	public class SimplexSolver
	{
		Cplex Cplex { get; }
		INumVar[] NumVars { get; }
		IRange[] Ranges { get; }
        
		public SimplexSolver(
			string[] varnames,
			double[] lowerBounds,
			double[] upperBounds,
			double[] objectiveCoefficients)
		{
			Cplex = new Cplex();
			NumVars = Cplex.NumVarArray(varnames.Length, lowerBounds, upperBounds);
			var linearNumExpr = Cplex.ScalProd(NumVars, objectiveCoefficients);
			Cplex.AddMaximize(linearNumExpr);
			Ranges = new IRange[2];
			Ranges[0] = Cplex.AddLe(Cplex.Sum(Cplex.Prod(-1.0, NumVars[0]),
			                                  Cplex.Prod(1.0, NumVars[1]),
			                                  Cplex.Prod(1.0, NumVars[2])), 30.0, "c1");
			Ranges[1] = Cplex.AddLe(Cplex.Sum(Cplex.Prod(1.0, NumVars[0]),
			                                  Cplex.Prod(-3.0, NumVars[1]),
			                                  Cplex.Prod(1.0, NumVars[2])), 40.0, "c2");
            Cplex.ExportModel("lpex1.lp");
		}

		public void PrintResults(){
			if (Cplex.Solve()){
				var values = Cplex.GetValues(NumVars);
				var reducedCosts = Cplex.GetReducedCosts(NumVars);
                var duals = Cplex.GetDuals(Ranges);
                var slacks = Cplex.GetSlacks(Ranges);

                Cplex.Output().WriteLine("Solution status = " + Cplex.GetStatus());
                Cplex.Output().WriteLine("Solution value  = " + Cplex.ObjValue);

                int nvars = values.Length;
                for (int j = 0; j < nvars; ++j)
                {
                    Cplex.Output().WriteLine("Variable   " + j +
                                             ": Value = " + values[j] +
                                             " Reduced cost = " + reducedCosts[j]);
                }

                int ncons = slacks.Length;
                for (int i = 0; i < ncons; ++i)
                {
                    Cplex.Output().WriteLine("Constraint " + i +
                                             ": Slack = " + slacks[i] +
                                             " Pi = " + duals[i]);
                }
			}

		}
	}
}
