using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
    public class Models
    {
        public static void RunStep(
            int c, int r, 
            ref double eq, ref double bo, ref double bi, 
            ref double curWithdrawal, 
            double initialWithdrawal,
            ref double[] prior, 
            ref List<double> withdrawals, 
            Model m, 
            Distro distroEq, Distro distroBo, Distro distroBi)
        {
            // Market play
            eq *= (1.0 + distroEq.Play());
            bo *= (1.0 + distroBo.Play());
            bi *= (1.0 + distroBi.Play());

            // For Strategy 3, we need to recalc withdrawals once a year based on prior 3 years
            if (m.Strategy == 3)
            {
                if (c >= 3 * (int)Utils.StepsInYear && c % ((int)Utils.StepsInYear) == 0)
                    curWithdrawal = prior.Average() * m.YearlyWithdrawal / 100.0 / Utils.StepsInYear;
            }
            // For Strategy 1, withdrawal is constant
            else if (m.Strategy == 1)
                curWithdrawal = initialWithdrawal;

            double actualWithdrawal = (eq + bo + bi >= curWithdrawal ? curWithdrawal : eq + bo + bi);
            double allocated = 0;

            // Withdrawal
            if (m.Strategy == 1 || m.Strategy == 3)
            {
                // Strategy 1 = calculate withdrawal at start and keep const
                // Strategy 3 = calculate withdrawal as percentage of average for the last 3 years
                if (bi > 0)
                {
                    double d = Math.Min(bi, curWithdrawal * (bi / (eq + bo + bi)));
                    allocated += d;
                    bi -= d;
                }
                if (bo > 0)
                {
                    double d = Math.Min(bo, curWithdrawal * (bo / (eq + bo + bi)));
                    allocated += d;
                    bo -= d;
                }
                if (eq > 0)
                {
                    double d = Math.Min(eq, curWithdrawal * (eq / (eq + bo + bi)));
                    allocated += d;
                    eq -= d;
                }

                if (allocated < actualWithdrawal)
                {
                    double d = Math.Min(bi, actualWithdrawal - allocated);
                    bi -= d;
                    allocated += d;

                    d = Math.Min(bo, actualWithdrawal - allocated);
                    bo -= d;
                    allocated += d;

                    d = Math.Min(eq, actualWithdrawal - allocated);
                    eq -= d;
                    allocated += d;
                }

                withdrawals.Add(actualWithdrawal);
            }
            else if (m.Strategy == 2)
            {
                // Strategy 2 = calculate withdrawal as percentage of current
                double w = (1.0 - m.YearlyWithdrawal / 100.0 / Utils.StepsInYear);
                withdrawals.Add((eq + bo + bi) * (1.0 - w));
                eq *= w;
                bo *= w;
                bi *= w;
            }

            double total = eq + bo + bi;

            // Rebalance evert X steps, if requested
            if (m.RebalanceEvery > 0 && r % m.RebalanceEvery == 0)
            {
                eq = total * m.StartEq / 100.0;
                bo = total * m.StartBo / 100.0;
                bi = total - eq - bo;
            }

            // Remember priors
            for (int y = 1; y < prior.Length; y++)
            {
                prior[y - 1] = prior[y];
            }
            prior[prior.Length - 1] = total;
        }

        public static List<SingleRunResult> RunSingle(
            GlobalParams globals,
            Model m, 
            Distro distroEq, Distro distroBo, Distro distroBi)
        {
            int rebalanceEvery = m.RebalanceEvery;
            double initialWithdrawal = globals.StartSum * m.YearlyWithdrawal / 100.0 / Utils.StepsInYear;
            double curWithdrawal = initialWithdrawal;
            double[] prior = new double[3 * (int)Utils.StepsInYear];

            List<SingleRunResult> singleRunResults = new List<SingleRunResult>();

            for (int r = 0; r < globals.Repeats; r++)
            {
                double eq = globals.StartSum * m.StartEq / 100;
                double bo = globals.StartSum * m.StartBo / 100;
                double bi = globals.StartSum * (100 - m.StartEq - m.StartBo) / 100;
                if (eq < 0 || bo < 0 || bi < 0)
                    throw new Exception("Bad parameters");

                List<double> withdrawals = new List<double>();
                for (int c = 0; c < globals.Cycles; c++)
                {
                    // Market play
                    RunStep(c, r, ref eq, ref bo, ref bi, 
                            ref curWithdrawal, initialWithdrawal,
                            ref prior, ref withdrawals, 
                            m, distroEq, distroBo, distroBi);
                }

                singleRunResults.Add(
                    new SingleRunResult(
                        eq + bo + bi,
                        withdrawals.Average(),
                        withdrawals.Min(),
                        withdrawals.Max()));
            }
            return singleRunResults;
        }

        public static List<SingleRunResult> RunDouble(
            GlobalParams globals,
            Model m, 
            double share2,
            Distro distroEq1, Distro distroBo1, Distro distroBi1,
            Distro distroEq2, Distro distroBo2, Distro distroBi2)
        {
            double withdrawal = m.YearlyWithdrawal / 100.0 / Utils.StepsInYear;
            int rebalanceEvery = m.RebalanceEvery;
            double initialWithdrawal = globals.StartSum * m.YearlyWithdrawal / 100.0 / Utils.StepsInYear;
            double[] prior = new double[3 * (int)Utils.StepsInYear];
            double curWithdrawal = initialWithdrawal;

            List<SingleRunResult> results = new List<SingleRunResult>();

            for (int r = 0; r < globals.Repeats; r++)
            {
                double eq1 = (1.0 - share2) * globals.StartSum * m.StartEq / 100;
                double bo1 = (1.0 - share2) * globals.StartSum * m.StartBo / 100;
                double bi1 = (1.0 - share2) * globals.StartSum * (100 - m.StartEq - m.StartBo) / 100;

                double eq2 = share2 * globals.StartSum * m.StartEq / 100;
                double bo2 = share2 * globals.StartSum * m.StartBo / 100;
                double bi2 = share2 * globals.StartSum * (100 - m.StartEq - m.StartBo) / 100;

                List<double> withdrawals = new List<double>();

                for (int c = 0; c < globals.Cycles; c++)
                {
                    // Market play
                    eq1 *= (1.0 + distroEq1.Play());
                    bo1 *= (1.0 + distroBo1.Play());
                    bi1 *= (1.0 + distroBi1.Play());

                    eq2 *= (1.0 + distroEq2.Play());
                    bo2 *= (1.0 + distroBo2.Play());
                    bi2 *= (1.0 + distroBi2.Play());

                    // For Strategy 3, we need to recalc withdrawals once a year based on prior 3 years
                    if (m.Strategy == 3)
                    {
                        if (c >= 3 * (int)Utils.StepsInYear && c % ((int)Utils.StepsInYear) == 0)
                            curWithdrawal = prior.Average() * m.YearlyWithdrawal / 100.0 / Utils.StepsInYear;
                    }
                    // For Strategy 1, withdrawal is constant
                    else if (m.Strategy == 1)
                        curWithdrawal = initialWithdrawal;

                    double curWithdrawal1 = (1.0 - share2) * curWithdrawal;
                    double curWithdrawal2 = (share2) * curWithdrawal;
                    //double actualWithdrawal1 = 0, actualWithdrawal2 = 0;

                    // Withdrawal
                    if (m.Strategy == 1 || m.Strategy == 3)
                    {
                        // Strategy 1 = calculate withdrawal at start and keep const
                        // Strategy 3 = calculate withdrawal as percentage of average for the last 3 years
                        if (eq1 > 0)
                        {
                            eq1 -= (curWithdrawal1 * (eq1 / (eq1 + bo1 + bi1)));
                            if (eq1 < 0)
                                eq1 = 0;
                        }
                        if (bo1 > 0)
                        {
                            bo1 -= (curWithdrawal1 * (bo1 / (eq1 + bo1 + bi1)));
                            if (bo1 < 0)
                                bo1 = 0;
                        }
                        if (bi1 > 0)
                        {
                            bi1 -= (curWithdrawal1 * (bi1 / (eq1 + bo1 + bi1)));
                            if (bi1 < 0)
                                bi1 = 0;
                        }

                        if (eq2 > 0)
                        {
                            eq2 -= (curWithdrawal2 * (eq2 / (eq2 + bo2 + bi2)));
                            if (eq2 < 0)
                                eq2 = 0;
                        }
                        if (bo2 > 0)
                        {
                            bo2 -= (curWithdrawal2 * (bo2 / (eq2 + bo2 + bi2)));
                            if (bo2 < 0)
                                bo2 = 0;
                        }
                        if (bi2 > 0)
                        {
                            bi2 -= (curWithdrawal2 * (bi2 / (eq2 + bo2 + bi2)));
                            if (bi2 < 0)
                                bi2 = 0;
                        }
                    
                    }
                    else if (m.Strategy == 2)
                    {
                        // Strategy 2 = calculate withdrawal as percentage of current
                        double w = (1.0 - m.YearlyWithdrawal / 100.0 / Utils.StepsInYear);
                        eq1 *= w;
                        bo1 *= w;
                        bi1 *= w;

                        eq2 *= w;
                        bo2 *= w;
                        bi2 *= w;
                    }

                    double total1 = eq1 + bo1 + bi1;
                    double total2 = eq2 + bo2 + bi2;

                    // Rebalance evert X steps, if requested
                    if (m.RebalanceEvery > 0 && r % m.RebalanceEvery == 0)
                    {
                        eq1 = total1 * m.StartEq / 100.0;
                        bo1 = total1 * m.StartBo / 100.0;
                        bi1 = total1 - eq1 - bo1;

                        eq2 = total2 * m.StartEq / 100.0;
                        bo2 = total2 * m.StartBo / 100.0;
                        bi2 = total2 - eq2 - bo2;
                    }

                    // Remember priors
                    for (int y = 1; y < prior.Length; y++)
                    {
                        prior[y - 1] = prior[y];
                    }
                    prior[prior.Length - 1] = total1 + total2;

                }
                results.Add( 
                    new SingleRunResult(
                        eq1 + bo1 + bi1 + eq2 + bo2 + bi2,
                        0, //         withdrawals.Average(),
                        0, // withdrawals.Min(),
                        0)); // withdrawals.Max()));

            }
            return results;
        }

        public static double Check(List<SingleRunResult> results, ref int failures, ref int successes)
        {
            failures = 0; 
            successes = 0;
            foreach (SingleRunResult r in results)
            {
                if (r.TrailingAmount <= 1000)
                    failures++;
                else
                    successes++;
            }
            if (successes + failures == 0)
                return 0.0;
            else
                return (double)successes / (double)(successes + failures);
        }
    }
}