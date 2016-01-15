using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

#if aa
namespace FinancialModelB
{
    class TopLevel
    {
        static void Main(string[] args)
        {
            if (args.Length >= 3 && args[2].ToLower() == "sweep")
            {
                Sweep(args);
                return;
            }
            else if (args.Length >= 3 && args[2].ToLower() == "sweepdual")
            {
                SweepDual(args);
                return;
            }

            else if (args.Length != 2)
            {
                Console.WriteLine("Usage:  countries models [sweep | sweeDual]\n");
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

        static void SweepDual(string[] args)
        {
            List<Model> models = Model.ReadModels(args[1]);
            List<Country> countries = Country.ReadCountries(args[0], false);

            // We respect Worlds's weight as starting sum world share, but for the countries we will use First Country's weight 
            double worldShare   = (double)countries.Last().Weight  / (double)(countries.Last().Weight + countries.First().Weight);
            double countryShare = (double)countries.First().Weight / (double)(countries.Last().Weight + countries.First().Weight);
            string worldName = countries.Last().Filename;

            string resFile = Params.ResultFileName("Results");
            if (!File.Exists(resFile))
            {
                using (StreamWriter sw = new StreamWriter(resFile))
                {
                    sw.WriteLine("Country,Withdrawal,Aver,SuccessRate, ");
                }
            }

            List<int> equityChangesWorld = new List<int>();
            List<int> bondChangesWorld = new List<int>();
            List<int> billChangesWorld = new List<int>();
            Distro distroEquitiesWorld = new Distro(Params.Bins);
            Distro distroBondsWorld = new Distro(Params.Bins);
            Distro distroBillsWorld = new Distro(Params.Bins);

            foreach (var c in countries)
            {
                if (c.Filename != worldName)
                    c.Weight = 0;
                else 
                    c.Weight = 1;
            }
            GraphAcquierer.Acquire(countries, equityChangesWorld, bondChangesWorld, billChangesWorld);
            Distro.PrepareDistribution(equityChangesWorld, distroEquitiesWorld, Params.Bins, "DistroEquitiesWorld.csv");
            Distro.PrepareDistribution(bondChangesWorld, distroBondsWorld, Params.Bins, "DistroBondsWorld.csv");
            Distro.PrepareDistribution(billChangesWorld, distroBillsWorld, Params.Bins, "DistroBillsWorld.csv");

            foreach (var c in countries)
               c.Weight = 0;

            foreach (var c in countries)
            {
                if (c.Filename == worldName)
                    continue;

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
                double[] endingTotalsCountry = new double[withdrawals.Length];
                double[] endingTotalsWorld= new double[withdrawals.Length];
                int[] indexes = new int[withdrawals.Length];
                for (int i = 0; i < withdrawals.Length; i++)
                {
                    indexes[i] = i;
                    withdrawals[i] = i * 0.5;
                    successRates[i] = -1.0;
                    endingTotalsCountry[i] = -1.0;
                    endingTotalsWorld[i] = -1.0;
                }

                ParallelLoopResult res1 = Parallel.ForEach(
                        indexes,
                        (index) =>
                        {
                            Model m = models[0];
                            m.YearlyWithdrawal = withdrawals[index];

                            int startingSum = m.StartSum;

                            m.StartSum = (int) (startingSum * countryShare);
                            List<double> resultCountry = Models.Run(m, distroEquities, distroBonds, distroBills);

                            m.StartSum = (int)(startingSum * worldShare);
                            List<double> resultWorld = Models.Run(m, distroEquitiesWorld, distroBondsWorld, distroBillsWorld);

                            List<double> result = new List<double>();
                            for (int i = 0; i < resultWorld.Count; i++ )
                                result.Add(resultCountry[i] + resultWorld[i]);

                            int failures = 0, successes = 0;
                            successRates[index] = Models.Check(result, ref failures, ref successes);
                            endingTotalsCountry[index] = result.Average();
                        });

                using (StreamWriter sw = new StreamWriter(resFile, true))
                {
                    foreach (int ind in indexes)
                    {
                        sw.WriteLine("{0},{1:F2},{2:F2},{3:F2},",
                            c.Filename.Replace(".JPG", ""),
                            withdrawals[ind],
                            (endingTotalsCountry[ind] + endingTotalsWorld[ind]) / 1000000.0,
                            successRates[ind]);
                    }
                }

                c.Weight = 0;
            }
        }

    }
}

                    /*
                            sw.WriteLine(
                                "{0},{1},{2},{3},{4},{5},{6},{7:F0},{8},{9},{10},{11},{12:F2},{13:F2},{14:F2},{15:F2},",
                                m.Strategy,
                                m.StrategyParameter1,
                                m.StrategyParameter2,
                                m.StrategyParameter3,
                                m.Comment,
                                m.Cycles,
                                m.Repeats,
                                m.StartSum / 1000000.0,
                                m.StartEq,
                                m.StartBo,
                                m.YearlyWithdrawal,
                                m.RebalanceEvery,
                                result.Average() / 1000000.0,
                                result.Max() / 1000000.0,
                                result.Min() / 1000000.0,
                                (double)((double)successes / (double)(successes + failures)));
                     * */


#endif