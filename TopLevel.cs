using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;

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

            for (int f = 0; f < factors.Length; f++)
                factors[f] = Factor.None;

            // Global params file
            if (args.Length <= cp)
            {
                Console.Write("Usage: <params.csv> <countries.csv> <models.csv> [single|dual] [sweep N factor-1 ... factor-n]");
                return;
            }
            GlobalParams globals = GlobalParams.ReadParams(args[cp++]);
            string resultPrefix = "R_" + globals.Prefix;

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
                    Console.WriteLine("The whole portfolio is managed as one thing");
                    portfolio = Portfolio.Single;
                    resultPrefix += "_Single";
                }
                else if (args[cp].ToLower() == "double")
                {
                    portfolio = Portfolio.Double;
                    Console.WriteLine("The portfolio is composed of 2 separate parts: all countries except last, and last");
                    resultPrefix += "_Double";
                }
                else
                {
                    Console.WriteLine("First parameter can be only 'single' or 'double'. It defines portfolio composition");
                    return;
                }
                cp++;
            }

            // Sweep mode
            if (args.Length > cp)
            {
                if (args[cp].ToLower() != "sweep")
                {
                    Console.WriteLine("This parameter can be only 'sweep'. It would request sweep by few listed parameters.");
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

            // Run simulations
            Execute(
                globals,
                countries,
                models,
                portfolio,
                sweepMode,
                sweeps,
                Params.ResultFileName(resultPrefix));
        }

        static void Execute(
            GlobalParams globals,
            List<Country> countries,
            List<Model> models,
            Portfolio portfolio,
            SweepMode sweepMode,
            SweepParameters[] sweeps,
            string resFile)
        {
            ConcurrentBag<ModelResult> modelResults = new ConcurrentBag<ModelResult>();
            Object printLock = new Object();

            if (sweepMode == SweepMode.No)
            {
                if (portfolio == Portfolio.Single)
                {
                    ExecuteSingle(globals, countries, models, modelResults, printLock);
                }
                else if (portfolio == Portfolio.Double)
                {
                    ExecuteDouble(globals, countries, models, modelResults, printLock);
                }
            }
            else if (sweepMode == SweepMode.SweepNoCountry)
            {
                if (portfolio == Portfolio.Single)
                {
                    ExecuteSweepSingle(globals, countries, models, sweeps, modelResults, printLock);
                }
                else if (portfolio == Portfolio.Double)
                {
                    ExecuteSweepDouble(globals, countries, models, sweeps, modelResults, printLock);
                }
            }
            else if (sweepMode == SweepMode.SweepAndCountry)
            {
                if (portfolio == Portfolio.Single)
                {
                    ExecuteSweepSingleByCountry(globals, countries, models, sweeps, modelResults, printLock);
                }
                else if (portfolio == Portfolio.Double)
                {
                    ExecuteSweepDoubleByCountry(globals, countries, models, sweeps, modelResults, printLock);
                }
            }

            IEnumerable<ModelResult> sortedResults = modelResults.OrderBy(
                mr => ((mr.model.Strategy * 100 + 
                        mr.model.StartEq) * 100 + 
                        mr.model.StartBo) * 100 + 
                        mr.model.YearlyWithdrawal);

            using (StreamWriter sw = new StreamWriter(resFile))
            {
                sw.WriteLine("Strategy,Eq,Bo,Withdrawal,Rebalance,TrailAver,TrailMax,TrailMin,WDAver,WDMax,WDMin,SuccessRate, ");
                foreach(ModelResult mr in sortedResults)
                {
                    sw.WriteLine(
                        "{0},{1},{2},{3:F2},{4},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11:F2},",
                        mr.model.Strategy,
                        mr.model.StartEq,
                        mr.model.StartBo,
                        mr.model.YearlyWithdrawal,
                        mr.model.RebalanceEvery,
                        mr.trailAverage,
                        mr.trailMax,
                        mr.trailMin,
                        mr.withdrawalAverage,
                        mr.withdrawalMax,
                        mr.withdrawalMin,
                        mr.trailSuccessRate);
                }
            }
        }

        // One run for a single portfolio
        static void ExecuteSingle(
            GlobalParams globals,
            List<Country> countries,
            List<Model> models,
            ConcurrentBag<ModelResult> modelResults,
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
                    List<SingleRunResult> result = Models.RunSingle(
                        globals, 
                        m,
                        distroEquities, distroBonds, distroBills);

                    modelResults.Add(new ModelResult(m, result));
                });
        }

        // One run for a 2-part portfolio
        static void ExecuteDouble(
            GlobalParams globals,
            List<Country> countries,
            List<Model> models,
            ConcurrentBag<ModelResult> modelResults,
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
                    List<SingleRunResult> result = Models.RunDouble(
                        globals,
                        m,
                        (double)countries.Last().Weight / (double)(countries.Last().Weight + countries.First().Weight),
                        distroEquities, distroBonds, distroBills,
                        distroEquitiesW, distroBondsW, distroBillsW);

                    modelResults.Add(new ModelResult(m, result));
                });
        }

        // Sweep run for a single portfolio
        static void ExecuteSweepSingle(
            GlobalParams globals,
            List<Country> countries,
            List<Model> models,
            SweepParameters[] sweeps,
            ConcurrentBag<ModelResult> modelResults,
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

            foreach(Model m in models)
            {
                ParallelLoopResult res1 = Parallel.ForEach(
                    sweeps,
                    (sw) =>
                    {
                        Model mm = Model.SweepModel(m, sw);
                        List<SingleRunResult> result = Models.RunSingle(
                            globals,
                            mm,
                            distroEquities, distroBonds, distroBills);

                        modelResults.Add(new ModelResult(mm, result));
                    });
                }
        }

        // Sweep run for a double-part portfolio 
        static void ExecuteSweepDouble(
            GlobalParams globals,
            List<Country> countries,
            List<Model> models,
            SweepParameters[] sweeps,
            ConcurrentBag<ModelResult> modelResults,
            Object printlock)
        {

        }

        // Sweep run for a single portfolio by country
        static void ExecuteSweepSingleByCountry(
            GlobalParams globals,
            List<Country> countries,
            List<Model> models,
            SweepParameters[] sweeps,
            ConcurrentBag<ModelResult> modelResults,
            Object printlock)
        {

        }

        // Sweep run for a double-part portfolio  by country
        static void ExecuteSweepDoubleByCountry(
            GlobalParams globals,
            List<Country> countries,
            List<Model> models,
            SweepParameters[] sweeps,
            ConcurrentBag<ModelResult> modelResults,
            Object printlock)
        {

        }
    }
}
