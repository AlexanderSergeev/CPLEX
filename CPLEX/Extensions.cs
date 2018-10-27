using System;

namespace CPLEX
{
    public static class Extensions
    {
        public static bool IsInteger(this double value, double eps = 1e-4)
        {
            return Math.Abs(value % 1) < eps;
        }

        public static bool Almost(this double value, double source, double eps = 1e-4)
        {
            return Math.Abs(value - source) < eps;
        }
    }
}