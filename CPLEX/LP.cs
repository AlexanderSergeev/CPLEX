using ILOG.Concert;
using ILOG.CPLEX;
using System;

public class LP
{
    public static void Main(string[] args)
    {
        try
        {
            Cplex cplex = new Cplex();

            INumVar[][] var = new INumVar[1][];
            IRange[][] rng = new IRange[1][];

            // generates the problem by adding constraints
            PopulateByRow(cplex, var, rng);

            // write model to file
            cplex.ExportModel("lpex1.lp");

            // solve the model and display the solution if one was found
            if (cplex.Solve())
            {
                double[] x = cplex.GetValues(var[0]);
                double[] dj = cplex.GetReducedCosts(var[0]);
                double[] pi = cplex.GetDuals(rng[0]);
                double[] slack = cplex.GetSlacks(rng[0]);

                cplex.Output().WriteLine("Solution status = " + cplex.GetStatus());
                cplex.Output().WriteLine("Solution value  = " + cplex.ObjValue);

                int nvars = x.Length;
                for (int j = 0; j < nvars; ++j)
                {
                    cplex.Output().WriteLine("Variable   " + j +
                                             ": Value = " + x[j] +
                                             " Reduced cost = " + dj[j]);
                }

                int ncons = slack.Length;
                for (int i = 0; i < ncons; ++i)
                {
                    cplex.Output().WriteLine("Constraint " + i +
                                             ": Slack = " + slack[i] +
                                             " Pi = " + pi[i]);
                }
            }
            cplex.End();
            Console.WriteLine("Press enter to close");
            Console.ReadLine();
        }
        catch (ILOG.Concert.Exception e)
        {
            Console.WriteLine("Concert exception '" + e + "' caught");
            Console.WriteLine("Press enter to close");
            Console.ReadLine();
        }
    }

    //    Maximize
    //     x1 + 4x2 + 2x3
    //    Subject To
    //     -x1 + x2 + x3 <= 30
    //     x1 - 3x2 + x3 <= 40
    //    Bounds
    //     0 <= x1 <= 50
    //     0 <= x2 <= 40
    //    End

    internal static void PopulateByRow(IMPModeler model,
                                       INumVar[][] var,
                                       IRange[][] rng)
    {
        double[] lb = { 0.0, 0.0, 0.0 };
        double[] ub = { 50.0, 40.0, double.MaxValue };
        string[] varname = { "x1", "x2", "x3" };
        INumVar[] x = model.NumVarArray(3, lb, ub, varname);
        var[0] = x;

        double[] objvals = { 1.0, 4.0, 2.0 };
        model.AddMaximize(model.ScalProd(x, objvals));

        rng[0] = new IRange[2];
        rng[0][0] = model.AddLe(model.Sum(model.Prod(-1.0, x[0]),
                                          model.Prod(1.0, x[1]),
                                          model.Prod(1.0, x[2])), 30.0, "c1");
        rng[0][1] = model.AddLe(model.Sum(model.Prod(1.0, x[0]),
                                          model.Prod(-3.0, x[1]),
                                          model.Prod(1.0, x[2])), 40.0, "c2");
    }
}
