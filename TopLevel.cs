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
            Portfolio portfolio = Portfolio.Single;
            SweepMode sweepMode = SweepMode.No;
            SweepParameters[] sweeps = new SweepParameters[1];
            int nFactors = 0;
            const int maxFactors = 5;
            Factor[] factors = new Factor[maxFactors];
            int cp = 0;
            string resultPrefix = "R";

            for (int f = 0; f < factors.Length; f++)
                factors[f] = Factor.None;

            // Countries file
            if (args.Length <= cp)
            {
                Console.Write("Usage: <countries.csv> <models.csv> [single|dual] [sweep N factor-1 ... factor-n]");
                return;
            }
            List<Country> countries = Country.ReadCountries(args[cp++], false);

            // Models file
            if (args.Length <= cp)
            {
                Console.Write("Second parameter must point to the models file");
                return;
            }
            List<Model> models = Model.ReadModels(args[cp++]);

            // Portfolio Composition Mode
            if (args.Length > cp)
            {
                if (args[cp].ToLower() == "single")
                {
                    Console.Write("The whole portfolio is managed as one thing");
                    portfolio = Portfolio.Single;
                    resultPrefix += "_Single";
                }
                else if (args[cp].ToLower() == "double")
                {
                    portfolio = Portfolio.Double;
                    Console.Write("The portfolio is composed of 2 separate parts: all countries except last, and last");
                    resultPrefix += "_Double";
                }
                else
                {
                    Console.Write("First parameter can be only 'single' or 'double'. It defines portfolio composition");
                    return;
                }
                cp++;
            }

            // Sweep mode
            if (args.Length > cp)
            {
                if (args[cp].ToLower() != "sweep")
                {
                    Console.Write("This parameter can be only 'sweep'. It would request sweep by few listed parameters.");
                    return;
                }
                else
                {
                    sweepMode = SweepMode.SweepNoCountry;
                    resultPrefix += "_Sweep";
                }
                cp++;
            }

            if (sweepMode != SweepMode.No)
            {
                // Sweep factors counter
                if (args.Length > cp)
                {
                    nFactors = int.Parse(args[cp]);
                    if (nFactors >= maxFactors)
                    {
                        Console.WriteLine("You cannot Sweep by more than {0} factors", maxFactors);
                        return;
                    }
                    Console.WriteLine("Sweep is requested for {0} factors", nFactors);
                    cp++;
                }
                else
                {
                    Console.WriteLine("This parameter can be only sweep factor count");
                    return;
                }
                for (int i = 0; i < nFactors; i++)
                {
                    //Country| Strategy | Withdrawal |  DualShare |  Eq | Bo 
                    switch (args[cp].ToLower())
                    {
                        case "country":
                            sweepMode = SweepMode.SweepAndCountry;
                            resultPrefix += "_Country";
                            break;
                        case "strategy":
                            factors[i] = Factor.Strategy;
                            resultPrefix += "_Strategy";
                            break;
                        case "worldshare":
                            factors[i] = Factor.WorldShare;
                            resultPrefix += "_WorldShare";
                            break;
                        case "withdrawal":
                            factors[i] = Factor.Withdrawal;
                            resultPrefix += "_Withdrawal";
                            break;
                        case "equity":
                            factors[i] = Factor.Equity;
                            resultPrefix += "_Equity";
                            break;
                        case "bonds":
                            factors[i] = Factor.Bonds;
                            resultPrefix += "_Bonds";
                            break;
                        default:
                            Console.Write("This parameter can be only Country| Strategy | Withdrawal |  WorldShare |  Equity | Bonds");
                            return;
                    }
                    cp++;
                }
            }


            // Prepare sweep parameters
            if (sweepMode != SweepMode.No)
            {
                sweeps = Params.Factorize(factors, countries);
                Console.WriteLine("You requested to sweep across {0} combinations", sweeps.Length);
            }

            Execute(
                countries,
                models,
                portfolio,
                sweepMode,
                sweeps,
                Params.ResultFileName(resultPrefix));
        }

        static void Execute(
            List<Country> countries,
            List<Model> models,
            Portfolio portfolio,
            SweepMode sweepMode,
            SweepParameters[] sweeps,
            string resFile)
        {
            Object printLock = new Object();
            if (!File.Exists(resFile))
            {
                using (StreamWriter sw = new StreamWriter(resFile))
                {
                    sw.WriteLine("Strategy,P1,P2,P3,Comment,Cycles,Repeats,StartSum,StartEq,StartBo,Withdrawal,Rebalance,Aver,Max,Min,SuccessRate, ");
                }
            }

            if (sweepMode != SweepMode.No)
            {
                if (portfolio == Portfolio.Single)
                {
                    ExecuteSweepSingle(countries, models, sweepMode, sweeps, resFile, printLock);
                }
                else if (portfolio == Portfolio.Double)
                {
                    ExecuteSweepDouble(countries, models, sweepMode, sweeps, resFile, printLock);
                }
            }
            else
            {
                if (portfolio == Portfolio.Single)
                {
                    ExecuteSingle(countries, models, resFile, printLock);
                }
                else if (portfolio == Portfolio.Double)
                {
                    ExecuteDouble(countries, models, resFile, printLock);
                }
            }
        }

        // One run for a single portfolio
        static void ExecuteSingle(
            List<Country> countries,
            List<Model> models,
            string resFile,
            Object printlock)
        {
            List<int> equityChanges = new List<int>();
            List<int> bondChanges = new List<int>();
            List<int> billChanges = new List<int>();

            GraphAcquierer.Acquire(countries, equityChanges, bondChanges, billChanges, printlock);

            Distro distroEquities = new Distro(Params.Bins);
            Distro distroBonds = new Distro(Params.Bins);
            Distro distroBills = new Distro(Params.Bins);

            Distro.Prepare(
                equityChanges, bondChanges, billChanges,
                distroEquities, distroBonds, distroBills,
                printlock);

            Distro.Test(
                distroEquities, distroBonds, distroBills,
                printlock);


            ParallelLoopResult res = Parallel.ForEach(
                models,
                (m) =>
                {
                    List<double> result = Models.RunSingle(
                        m,
                        distroEquities, distroBonds, distroBills);

                    int failures = 0, successes = 0;
                    double successRate = Models.Check(result, ref failures, ref successes);

                    lock (printlock)
                    {
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
                                m.StartSum / 1000000.0,
                                m.StartEq,
                                m.StartBo,
                                m.YearlyWithdrawal,
                                m.RebalanceEvery,
                                result.Average() / 1000000.0,
                                result.Max() / 1000000.0,
                                result.Min() / 1000000.0,
                                (double)((double)successes / (double)(successes + failures)));
                        }
                    }
                });
        }

        // One run for a 2-part portfolio
        static void ExecuteDouble(
            List<Country> countries,
            List<Model> models,
            string resFile,
            Object printlock)
        {
            List<Country> world = new List<Country>();
            world.Add(countries.Last());

            List<int> equityChangesW = new List<int>();
            List<int> bondChangesW = new List<int>();
            List<int> billChangesW = new List<int>();

            GraphAcquierer.Acquire(world, equityChangesW, bondChangesW, billChangesW, printlock);

            Distro distroEquitiesW = new Distro(Params.Bins);
            Distro distroBondsW = new Distro(Params.Bins);
            Distro distroBillsW = new Distro(Params.Bins);

            Distro.Prepare(
                equityChangesW, bondChangesW, billChangesW,
                distroEquitiesW, distroBondsW, distroBillsW,
                printlock);


            List<Country> other = countries;
            other.RemoveAt(countries.Count - 1);

            List<int> equityChanges = new List<int>();
            List<int> bondChanges = new List<int>();
            List<int> billChanges = new List<int>();

            GraphAcquierer.Acquire(countries, equityChanges, bondChanges, billChanges, printlock);

            Distro distroEquities = new Distro(Params.Bins);
            Distro distroBonds = new Distro(Params.Bins);
            Distro distroBills = new Distro(Params.Bins);

            Distro.Prepare(
                equityChanges, bondChanges, billChanges,
                distroEquities, distroBonds, distroBills,
                printlock);


            ParallelLoopResult res = Parallel.ForEach(
                models,
                (m) =>
                {
                    List<double> result = Models.RunDouble(
                        m,
                        (double)countries.Last().Weight / (double)(countries.Last().Weight + countries.First().Weight),
                        distroEquities, distroBonds, distroBills,
                        distroEquitiesW, distroBondsW, distroBillsW);

                    int failures = 0, successes = 0;
                    double successRate = Models.Check(result, ref failures, ref successes);

                    lock (printlock)
                    {
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
                                m.StartSum / 1000000.0,
                                m.StartEq,
                                m.StartBo,
                                m.YearlyWithdrawal,
                                m.RebalanceEvery,
                                result.Average() / 1000000.0,
                                result.Max() / 1000000.0,
                                result.Min() / 1000000.0,
                                (double)((double)successes / (double)(successes + failures)));
                        }
                    }
                });
        }

        // Sweep run for a single portfolio
        static void ExecuteSweepSingle(
            List<Country> countries,
            List<Model> models,
            SweepMode sweepMode,
            SweepParameters[] sweeps,
            string resFile,
            Object printlock)
        {

        }

        // Sweep run for a double-part portfolio 
        static void ExecuteSweepDouble(
            List<Country> countries,
            List<Model> models,
            SweepMode sweepMode,
            SweepParameters[] sweeps,
            string resFile,
            Object printlock)
        {

        }
    }
}
