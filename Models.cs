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
        public static double NormativeStepWD(Model m)
        {
            return m.YearlyWithdrawal / 100.0 / Utils.StepsInYear;
        }
        public static void RunSingleStep(
            int c, int r, Model m, double initialWithdrawal,
            ref double eq, ref double bo, ref double bi, 
            Distro distroEq, Distro distroBo, Distro distroBi,
            ref double[] prior, 
            ref List<double> withdrawals)
        {
            // Market play
            eq *= (1.0 + distroEq.Play());
            bo *= (1.0 + distroBo.Play());
            bi *= (1.0 + distroBi.Play());

            // Calculate desired withdrawal on this step
            double curWithdrawal = initialWithdrawal;
            switch(m.Strategy)
            {
                case 1:
                    break;
                case 2:
                    curWithdrawal = (eq + bo + bi) * NormativeStepWD(m);
                    break;
                case 3:
                    if (c >= 3 * (int)Utils.StepsInYear && c % ((int)Utils.StepsInYear) == 0)
                        curWithdrawal = prior.Average() * NormativeStepWD(m);
                    break;
                default:
                    throw new Exception("Unknown strategy");
            }

            // Calculate actual total step withdrawal
            double actualWithdrawal = Math.Min(eq + bo + bi, curWithdrawal);
            double allocated = 0;

            if (bi > 0)
            {
                double d = Math.Min(bi, actualWithdrawal * (bi / (eq + bo + bi)));
                allocated += d;
                bi -= d;
            }
            if (bo > 0)
            {
                double d = Math.Min(bo, actualWithdrawal * (bo / (eq + bo + bi)));
                allocated += d;
                bo -= d;
            }
            if (eq > 0)
            {
                double d = Math.Min(eq, actualWithdrawal * (eq / (eq + bo + bi)));
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

        public static List<SingleRunResult> RunSinglePortfolioExperiment(
            GlobalParams globals,
            Model m, 
            Distro distroEq, Distro distroBo, Distro distroBi)
        {
            int rebalanceEvery = m.RebalanceEvery;
            double initWD = globals.StartSum * NormativeStepWD(m);
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
                    RunSingleStep(c, r, m, initWD, 
                                  ref eq, ref bo, ref bi, 
                                  distroEq, distroBo, distroBi, 
                                  ref prior, ref withdrawals);
                }

                singleRunResults.Add(new SingleRunResult(globals, "", m, eq + bo + bi, withdrawals.ToArray()));
            }
            return singleRunResults;
        }

        public static List<SingleRunResult> RunDoublePortfolioExperiment(
            GlobalParams globals,
            Model m, 
            double share2,
            Distro distroEq1, Distro distroBo1, Distro distroBi1,
            Distro distroEq2, Distro distroBo2, Distro distroBi2)
        {
            int rebalanceEvery = m.RebalanceEvery;
            double initWD = globals.StartSum * NormativeStepWD(m);
            double initWD1 = initWD * (1.0 - share2);
            double initWD2 = initWD * (share2);
            double curWD1 = initWD1;
            double curWD2 = initWD2;
            double[] prior1 = new double[3 * (int)Utils.StepsInYear];
            double[] prior2 = new double[3 * (int)Utils.StepsInYear];

            List<SingleRunResult> results = new List<SingleRunResult>();

            for (int r = 0; r < globals.Repeats; r++)
            {
                double eq1 = (1.0 - share2) * globals.StartSum * m.StartEq / 100;
                double bo1 = (1.0 - share2) * globals.StartSum * m.StartBo / 100;
                double bi1 = (1.0 - share2) * globals.StartSum * (100 - m.StartEq - m.StartBo) / 100;
                if (eq1 < 0 || bo1 < 0 || bi1 < 0)
                    throw new Exception("Bad parameters");

                double eq2 = share2 * globals.StartSum * m.StartEq / 100;
                double bo2 = share2 * globals.StartSum * m.StartBo / 100;
                double bi2 = share2 * globals.StartSum * (100 - m.StartEq - m.StartBo) / 100;
                if (eq2 < 0 || bo2 < 0 || bi2 < 0)
                    throw new Exception("Bad parameters");

                //List<double> withdrawals = new List<double>();
                List<double> withdrawals1 = new List<double>();
                List<double> withdrawals2 = new List<double>();

                for (int c = 0; c < globals.Cycles; c++)
                {

                    RunSingleStep(c, r, m, initWD1,
                                  ref eq1, ref bo1, ref bi1,
                                  distroEq1, distroBo1, distroBi1,
                                  ref prior1, ref withdrawals1);

                    RunSingleStep(c, r, m, initWD2,
                                  ref eq2, ref bo2, ref bi2,
                                  distroEq2, distroBo2, distroBi2,
                                  ref prior2, ref withdrawals2);

                    //TODO: here comes portfolio parts re balancing
                }

                results.Add( 
                    new SingleRunResult(
                        globals, m.CountryName, m, 
                        eq1 + bo1 + bi1 + eq2 + bo2 + bi2, 
                        withdrawals1.ToArray(), 
                        withdrawals2.ToArray()));

            }
            return results;
        }

        public static double CheckTrailingAmount(GlobalParams globals, 
                                   List<SingleRunResult> results, 
                                   ref int failures, ref int successes)
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

        public static double CheckWithdrawals(GlobalParams globals,
                                   List<SingleRunResult> results,
                                   ref int failures, ref int successes)
        {
            failures = 0;
            successes = 0;
            foreach (SingleRunResult r in results)
            {
                if (r.InsufficientWdRrate > globals.AllowedInsufficientRate)
                    failures++;
                else
                    successes++;
            }
            if (successes + failures == 0)
                return 0.0;
            else
                return (double)successes / (double)(successes + failures);
        }

        public static double CheckOverall(GlobalParams globals,
                                   List<SingleRunResult> results,
                                   ref int failures, ref int successes)
        {
            failures = 0;
            successes = 0;
            foreach (SingleRunResult r in results)
            {
                if (r.TrailingAmount <= 1000 ||
                    r.InsufficientWdRrate > globals.AllowedInsufficientRate)
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