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
        public static List<double> Run(Model m, Distro distroEq, Distro distroBo, Distro distroBi)
        {
            double withdrawal = m.YearlyWithdrawal / 100.0 / Params.StepsInYear;
            int rebalanceEvery = m.RebalanceEvery;
            double initialWithdrawal = m.StartSum * m.YearlyWithdrawal / 100.0 / Params.StepsInYear;
            double[] prior = new double[3 * (int)Params.StepsInYear];
            double curWithdrawal = initialWithdrawal; 

            List<double> results = new List<double>();
            for (int r = 0; r < m.Repeats; r++)
            {
                double eq = m.StartSum * m.StartEq / 100;
                double bo = m.StartSum * m.StartBo / 100;
                double bi = m.StartSum * (100 - m.StartEq - m.StartBo) / 100;

                for (int c = 0; c < m.Cycles; c++)
                {
                    // Market play
                    eq *= (1.0 + distroEq.Play());
                    bo *= (1.0 + distroBo.Play());
                    bi *= (1.0 + distroBi.Play());

                    // For Strategy 3, we need to recalc withdrawals once a year based on prior 3 years
                    if (m.Strategy == 3)
                    {
                        if (c >= 3 * (int)Params.StepsInYear && c % ((int)Params.StepsInYear) == 0)
                            curWithdrawal = prior.Average() * m.YearlyWithdrawal / 100.0 / Params.StepsInYear;
                    }
                    // For Strategy 1, withdrawal is constant
                    else if (m.Strategy == 1)
                        curWithdrawal = initialWithdrawal;

                    // Withdrawal
                    if (m.Strategy == 1 || m.Strategy == 3)
                    {
                        // Strategy 1 = calculate withdrawal at start and keep const
                        // Strategy 3 = calculate withdrawal as percentage of average for the last 3 years
                        if (eq > 0)
                        {
                            eq -= (curWithdrawal * (eq / (eq + bo + bi)));
                            if (eq < 0)
                                eq = 0;
                        }
                        if (bo > 0)
                        {
                            bo -= (curWithdrawal * (bo / (eq + bo + bi)));
                            if (bo < 0)
                                bo = 0;
                        }
                        if (bi > 0)
                        {
                            bi -= (curWithdrawal * (bi / (eq + bo + bi)));
                            if (bi < 0)
                                bi = 0;
                        }
                    }
                    else if (m.Strategy == 2)
                    {
                        // Strategy 2 = calculate withdrawal as percentage of current
                        double w = (1.0 - m.YearlyWithdrawal / 100.0 / Params.StepsInYear);
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
                        prior[y-1] = prior[y];
                    }
                    prior[prior.Length - 1] = total;

                }
                results.Add(eq + bo + bi);
            }
            return results;
        }

        public static double Check(List<double> results, ref int failures, ref int successes)
        {
            failures = 0; 
            successes = 0;
            foreach (double r in results)
            {
                if (r <= 1000)
                    failures++;
                else
                    successes++;
            }
            if (successes + failures == 0)
                return 0.0;
            else
                return (double)successes / (double)(successes + failures);
        }

        public static void Assess(Model m, List<double> results, string resFile)
        {
            int failures = 0, successes = 0;
            double successRate = Check(results, ref failures , ref successes);

            Console.WriteLine("{0} sucesses, {1} failures", successes, failures);
            if (!File.Exists(resFile))
            {
                using (StreamWriter sw = new StreamWriter(resFile))
                {
                    sw.WriteLine("Strategy,P1,P2,P3,Comment,Cycles,Repeats,StartSum,StartEq,StartBo,Withdrawal,Rebalance,Aver,Max,Min,SuccessRate, ");
                }
            }
            using (StreamWriter sw = new StreamWriter(resFile, true))
            {
                sw.WriteLine(
                    "{0},{1},{2},{3},{4},{5},{6},{7:F0},{8},{9},{10},{11},{12:F2},{13:F2},{14:F2},{15:F2},",
                    m.Strategy, 
                    m.StrategyParameter1, 
                    m.StrategyParameter2, 
                    m.StrategyParameter3,
                    m.Comment,
                    m.Cycles,
                    m.Repeats,
                    m.StartSum/1000000.0,
                    m.StartEq,
                    m.StartBo,
                    m.YearlyWithdrawal,
                    m.RebalanceEvery,
                    results.Average() / 1000000.0,
                    results.Max() / 1000000.0,
                    results.Min() / 1000000.0,
                    (double)((double)successes / (double)(successes + failures)));
            }

        }
    }
}