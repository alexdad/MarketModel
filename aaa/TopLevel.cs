using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
    class TopLevel
    {
        static void Main(string[] args)
        {
            if (args.Length >= 3 && args[2].ToLower().StartsWith("sweep"))
            {
                Sweep(args);
                return;
            }
            else if (args.Length != 2)
            {
                Console.WriteLine("Usage:  countries models OR sweep\n");
                return;
            }
            RunModel(args);
        }

        static void RunModel(string[] args)
        {
            List<int> equityChanges = new List<int>();
            List<int> bondChanges = new List<int>();
            List<int> billChanges = new List<int>();

            List<Country> countries = Country.ReadCountries(args[0], true);

            GraphAcquierer.Acquire(countries, equityChanges, bondChanges, billChanges);

            Distro distroEquities = new Distro(Params.Bins);
            Distro distroBonds = new Distro(Params.Bins);
            Distro distroBills = new Distro(Params.Bins);

            Distro.PrepareDistribution(equityChanges, distroEquities, Params.Bins, "DistroEquities.csv");
            Distro.PrepareDistribution(bondChanges, distroBonds, Params.Bins, "DistroBonds.csv");
            Distro.PrepareDistribution(billChanges, distroBills, Params.Bins, "DistroBills.csv");

            int[] tests = {1,2,3};
            ParallelLoopResult res1 = Parallel.ForEach(
                    tests,
                    (t) =>
                    {
                        List<int> testValues = new List<int>();
                        Distro distroTest = new Distro(Params.Bins);
                        switch (t)
                        {
                            case 1:
                                for (int i = 0; i < 10000000; i++)
                                    testValues.Add((int)(distroEquities.Play() * Params.PercentageScale));
                                Distro.PrepareDistribution(testValues, distroTest, Params.Bins, "testEq.csv");
                                break;
                            case 2:
                                for (int i = 0; i < 10000000; i++)
                                    testValues.Add((int)(distroBonds.Play() * Params.PercentageScale));
                                Distro.PrepareDistribution(testValues, distroTest, Params.Bins, "testBo.csv");
                                break;
                            case 3:
                                for (int i = 0; i < 10000000; i++)
                                    testValues.Add((int)(distroBills.Play() * Params.PercentageScale));
                                Distro.PrepareDistribution(testValues, distroTest, Params.Bins, "testBi.csv");
                                break;
                        }
                    });


            string resultFile = Params.ResultFileName("Results");

            {
                List<Model> models = Model.ReadModels(args[1]);
                Object printLock = new Object();

                ParallelLoopResult res = Parallel.ForEach(
                    models,
                    (m) =>
                    {
                        List<double> result = Models.Run(m, distroEquities, distroBonds, distroBills);
                        lock (printLock)
                        {
                            Models.Assess(m, result, resultFile);
                        }
                    });
            }

        }

        static void Sweep(string[] args)
        {
            List<Country> countries = Country.ReadCountries(args[0], false);
            
            Country cWorld = countries[0];
            foreach (var c in countries)
                c.Weight = 0;

            List<Model> models = Model.ReadModels(args[1]);

            string resFile = Params.ResultFileName("Results");
            if (!File.Exists(resFile))
            {
                using (StreamWriter sw = new StreamWriter(resFile))
                {
                    sw.WriteLine("Country,Withdrawal,Aver,SuccessRate, ");
                }
            }

            foreach (var c in countries)
            {
                c.Weight = 1;

                List<int> equityChanges = new List<int>();
                List<int> bondChanges = new List<int>();
                List<int> billChanges = new List<int>();

                GraphAcquierer.Acquire(countries, equityChanges, bondChanges, billChanges);

                Distro distroEquities = new Distro(Params.Bins);
                Distro distroBonds = new Distro(Params.Bins);
                Distro distroBills = new Distro(Params.Bins);

                Distro.PrepareDistribution(equityChanges, distroEquities, Params.Bins, "DistroEquities.csv");
                Distro.PrepareDistribution(bondChanges, distroBonds, Params.Bins, "DistroBonds.csv");
                Distro.PrepareDistribution(billChanges, distroBills, Params.Bins, "DistroBills.csv");

                double[] withdrawals = new double[20];
                double[] successRates = new double[withdrawals.Length];
                double[] endingTotals = new double[withdrawals.Length];
                int[] indexes = new int[withdrawals.Length];
                for (int i=0; i < withdrawals.Length; i++)
                {
                    indexes[i] = i;
                    withdrawals[i] = i * 0.5;
                    successRates[i] = -1.0;
                    endingTotals[i] = -1.0;
                }

                ParallelLoopResult res1 = Parallel.ForEach(
                        indexes,
                        (index) =>
                        {
                            Model m = models[0];
                            m.YearlyWithdrawal = withdrawals[index];

                            List<double> result = Models.Run(m, distroEquities, distroBonds, distroBills);

                            int failures = 0, successes = 0;
                            successRates[index] = Models.Check(result, ref failures, ref successes);
                            endingTotals[index] = result.Average();
                        });

                using (StreamWriter sw = new StreamWriter(resFile, true))
                {
                    foreach (int ind in indexes)
                    {
                        sw.WriteLine("{0},{1:F2},{2:F2},{3:F2},",
                            c.Filename.Replace(".JPG", ""),
                            withdrawals[ind],
                            endingTotals[ind] / 1000000.0,
                            successRates[ind]);
                    }
                }

                c.Weight = 0;
            }
        }
    }
}
