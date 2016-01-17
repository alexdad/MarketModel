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
                if (args[cp].ToLower().Trim() == "single")
                {
                    Console.WriteLine("The whole portfolio is managed as one thing");
                    portfolio = Portfolio.Single;
                    resultPrefix += "_Single";
                }
                else if (args[cp].ToLower().Trim() == "double")
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
                if (args[cp].ToLower().Trim() != "sweep")
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
                    switch (args[cp].ToLower().Trim())
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
                sweeps = Utils.Factorize(globals, factors, countries);
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
                Utils.ResultFileName(resultPrefix));
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
                mr => ((mr.model.CountryName + 
                        (mr.model.Strategy * 100).ToString() +
                        (mr.model.StartEq) * 100).ToString() +
                        (mr.model.StartBo) * 100).ToString() +
                        (mr.model.YearlyWithdrawal).ToString());

            Dictionary<string, double> cutoffs = new Dictionary<string, double>();

            using (StreamWriter sw = new StreamWriter(resFile))
            {
                sw.WriteLine(Utils.ResultHeader);

                foreach(ModelResult mr in sortedResults)
                {
                    Utils.WriteResult(sw, mr, printLock);

                    if (mr.trailSuccessRate >= globals.CutoffPercent / 100.0)
                    {
                        // Assuming sorted order!
                        if (cutoffs.ContainsKey(mr.model.CountryName))
                            cutoffs[mr.model.CountryName] = mr.model.YearlyWithdrawal;
                        else
                            cutoffs.Add(mr.model.CountryName, mr.model.YearlyWithdrawal);
                    }
                }
            }

            using (StreamWriter sw = new StreamWriter("Summary_" + resFile))
            {
                foreach(string c in cutoffs.Keys)
                {
                    sw.WriteLine("{0},{1:F1}", c, cutoffs[c]);
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

            Distro distroEquities = new Distro(globals.Bins);
            Distro distroBonds = new Distro(globals.Bins);
            Distro distroBills = new Distro(globals.Bins);

            Distro.Prepare(
                globals,
                equityChanges, bondChanges, billChanges,
                distroEquities, distroBonds, distroBills,
                printlock);

            Distro.Test(
                globals,
                distroEquities, distroBonds, distroBills,
                printlock);

            lock (printlock)
            {
                Console.WriteLine(Utils.ResultHeader);
            }

            var res = Parallel.ForEach(
                models,
                (m) =>
                {
                    if (m.Validate())
                    {
                        List<SingleRunResult> result = Models.RunSinglePortfolioExperiment(
                            globals,
                            m,
                            distroEquities, distroBonds, distroBills);

                        ModelResult mr = new ModelResult(globals, m, result);
                        modelResults.Add(mr);
                        Utils.WriteResult(null, m, mr, printlock);
                    }
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
            // Separate countries into 2 groups: 2=WORLD, 1=all others
            double weight1 = 0.0, weight2 = 0.0; 
            List<Country> countries1 = new List<Country>();
            List<Country> countries2 = new List<Country>();
            foreach (Country c in countries)
            {
                if (c.Filename.ToLower().Trim() == globals.DoubleWorldName.ToLower().Trim())
                {
                    countries2.Add(c);
                    weight2 += c.Weight;
                }
                else
                {
                    countries1.Add(c);
                    weight1 += c.Weight;
                }
            }
            if (weight1 <= 0 || weight2 <= 0)
                throw new Exception("Cannot find the world or others"); 

            // Group2 is just the World
            List<int> equityChanges2 = new List<int>();
            List<int> bondChanges2 = new List<int>();
            List<int> billChanges2 = new List<int>();

            GraphAcquierer.Acquire(countries2, equityChanges2, bondChanges2, billChanges2, printlock);

            Distro distroEquities2 = new Distro(globals.Bins);
            Distro distroBonds2 = new Distro(globals.Bins);
            Distro distroBills2 = new Distro(globals.Bins);

            Distro.Prepare(
                globals,
                equityChanges2, bondChanges2, billChanges2,
                distroEquities2, distroBonds2, distroBills2,
                printlock);

            // Group1 is all except World
            List<int> equityChanges1 = new List<int>();
            List<int> bondChanges1 = new List<int>();
            List<int> billChanges1 = new List<int>();

            GraphAcquierer.Acquire(countries1, equityChanges1, bondChanges1, billChanges1, printlock);

            Distro distroEquities1 = new Distro(globals.Bins);
            Distro distroBonds1 = new Distro(globals.Bins);
            Distro distroBills1 = new Distro(globals.Bins);

            Distro.Prepare(
                globals,
                equityChanges1, bondChanges1, billChanges1,
                distroEquities1, distroBonds1, distroBills1,
                printlock);

            lock (printlock)
            {
                Console.WriteLine(Utils.ResultHeader);
            }

            var res = Parallel.ForEach(
                models,
                (m) =>
                {
                    if (m.Validate())
                    {
                        List<SingleRunResult> result = Models.RunDoublePortfolioExperiment(
                            globals,
                            m,
                            weight2 / (weight1 + weight2),
                            distroEquities1, distroBonds1, distroBills1,
                            distroEquities2, distroBonds2, distroBills2);

                        ModelResult mr = new ModelResult(globals, m, result);
                        modelResults.Add(mr);
                        Utils.WriteResult(null, m, mr, printlock);

                    }
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
            Country c = new Country("", 0, 0, 0.0, 0.0, 0.0, 0);

            List<int> equityChanges = new List<int>();
            List<int> bondChanges = new List<int>();
            List<int> billChanges = new List<int>();

            GraphAcquierer.Acquire(countries, equityChanges, bondChanges, billChanges, printlock);

            Distro distroEquities = new Distro(globals.Bins);
            Distro distroBonds = new Distro(globals.Bins);
            Distro distroBills = new Distro(globals.Bins);

            Distro.Prepare(
                globals,
                equityChanges, bondChanges, billChanges,
                distroEquities, distroBonds, distroBills,
                printlock);

            Distro.Test(
                globals,
                distroEquities, distroBonds, distroBills,
                printlock);

            lock (printlock)
            {
                Console.WriteLine(Utils.ResultHeader);
            }

            var res2 = Parallel.ForEach(
                models,
                (m) =>
                {
                    var res1 = Parallel.ForEach(
                        sweeps,
                        (sw) =>
                        {
                            Model mm = Model.SweepModel(m, sw, c);
                            if (mm.Validate())
                            {
                                List<SingleRunResult> result = Models.RunSinglePortfolioExperiment(
                                    globals,
                                    mm,
                                    distroEquities, distroBonds, distroBills);

                                ModelResult mr = new ModelResult(globals, mm, result);
                                modelResults.Add(mr);
                                Utils.WriteResult(null, m, mr, printlock);
                            }
                        });
                });
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
            // Separate countries into 2 groups: 2=WORLD, 1=all others
            double weight1 = 0.0, weight2 = 0.0;
            List<Country> countries1 = new List<Country>();
            List<Country> countries2 = new List<Country>();
            foreach (Country c in countries)
            {
                if (c.Filename.ToLower().Trim() == globals.DoubleWorldName.ToLower().Trim())
                {
                    countries2.Add(c);
                    weight2 += c.Weight;
                }
                else
                {
                    countries1.Add(c);
                    weight1 += c.Weight;
                }
            }
            if (weight1 <= 0 || weight2 <= 0)
                throw new Exception("Cannot find the world or others");

            // Group2 is just the World
            List<int> equityChanges2 = new List<int>();
            List<int> bondChanges2 = new List<int>();
            List<int> billChanges2 = new List<int>();

            GraphAcquierer.Acquire(countries2, equityChanges2, bondChanges2, billChanges2, printlock);

            Distro distroEquities2 = new Distro(globals.Bins);
            Distro distroBonds2 = new Distro(globals.Bins);
            Distro distroBills2 = new Distro(globals.Bins);

            Distro.Prepare(
                globals,
                equityChanges2, bondChanges2, billChanges2,
                distroEquities2, distroBonds2, distroBills2,
                printlock);

            // Group1 is all except World
            List<int> equityChanges1 = new List<int>();
            List<int> bondChanges1 = new List<int>();
            List<int> billChanges1 = new List<int>();

            GraphAcquierer.Acquire(countries1, equityChanges1, bondChanges1, billChanges1, printlock);

            Distro distroEquities1 = new Distro(globals.Bins);
            Distro distroBonds1 = new Distro(globals.Bins);
            Distro distroBills1 = new Distro(globals.Bins);

            Distro.Prepare(
                globals,
                equityChanges1, bondChanges1, billChanges1,
                distroEquities1, distroBonds1, distroBills1,
                printlock);

            lock (printlock)
            {
                Console.WriteLine(Utils.ResultHeader);
            }

            var res2 = Parallel.ForEach(
                models,
                (m) =>
                {
                    var res1 = Parallel.ForEach(
                        sweeps,
                        (sw) =>
                        {
                            Country nullCountry = new Country();
                            Model mm = Model.SweepModel(m, sw, nullCountry);
                            if (mm.Validate())
                            {
                                List<SingleRunResult> result = Models.RunDoublePortfolioExperiment(
                                    globals,
                                    mm,
                                    weight2 / (weight1 + weight2),
                                    distroEquities1, distroBonds1, distroBills1,
                                    distroEquities2, distroBonds2, distroBills2);

                                ModelResult mr = new ModelResult(globals, mm, result);
                                modelResults.Add(mr);
                                Utils.WriteResult(null, m, mr, printlock);
                            }
                        });
                });
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
            foreach (var c in countries)
                c.Weight = 0;

            foreach (var c in countries)
            {
                c.Weight = 1;

                List<int> equityChanges = new List<int>();
                List<int> bondChanges = new List<int>();
                List<int> billChanges = new List<int>();

                GraphAcquierer.Acquire(countries, equityChanges, bondChanges, billChanges, printlock);

                Distro distroEquities = new Distro(globals.Bins);
                Distro distroBonds = new Distro(globals.Bins);
                Distro distroBills = new Distro(globals.Bins);

                Distro.Prepare(
                    globals,
                    equityChanges, bondChanges, billChanges,
                    distroEquities, distroBonds, distroBills,
                    printlock);

                Distro.Test(
                    globals,
                    distroEquities, distroBonds, distroBills,
                    printlock);

                lock (printlock)
                {
                    Console.WriteLine(Utils.ResultHeader);
                }

                var res2 = Parallel.ForEach(
                    models,
                    (m) =>
                    {
                        var res1 = Parallel.ForEach(
                            sweeps,
                            (sw) =>
                            {
                                Model mm = Model.SweepModel(m, sw, c);
                                if (mm.Validate())
                                {
                                    if (mm.StartEq + mm.StartBo <= 100)
                                    {
                                        List<SingleRunResult> result = Models.RunSinglePortfolioExperiment(
                                            globals,
                                            mm,
                                            distroEquities, distroBonds, distroBills);

                                        ModelResult mr = new ModelResult(globals, mm, result);
                                        modelResults.Add(mr);
                                        Utils.WriteResult(null, m, mr, printlock);
                                    }
                                }
                            });
                    });

                c.Weight = 0;
            }
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
            // Group2 is just the World
            List<Country> countries2 = new List<Country>();
            foreach (Country c in countries)
            {
                if (c.Filename.ToLower().Trim() == globals.DoubleWorldName.ToLower().Trim())
                {
                    countries2.Add(c);
                    countries2.Last().Weight = 1;
                }
            }

            List<int> equityChanges2 = new List<int>();
            List<int> bondChanges2 = new List<int>();
            List<int> billChanges2 = new List<int>();

            GraphAcquierer.Acquire(countries2, equityChanges2, bondChanges2, billChanges2, printlock);

            Distro distroEquities2 = new Distro(globals.Bins);
            Distro distroBonds2 = new Distro(globals.Bins);
            Distro distroBills2 = new Distro(globals.Bins);

            Distro.Prepare(
                globals,
                equityChanges2, bondChanges2, billChanges2,
                distroEquities2, distroBonds2, distroBills2,
                printlock);

            // Now enumerate countries; Group1 each time will carry just one
            foreach (var c in countries)
            {
                List<Country> countries1 = new List<Country>();
                countries1.Add(c);
                countries1.Last().Weight = 1;

                // Group1 is just one country
                List<int> equityChanges1 = new List<int>();
                List<int> bondChanges1 = new List<int>();
                List<int> billChanges1 = new List<int>();

                GraphAcquierer.Acquire(countries1, equityChanges1, bondChanges1, billChanges1, printlock);

                Distro distroEquities1 = new Distro(globals.Bins);
                Distro distroBonds1 = new Distro(globals.Bins);
                Distro distroBills1 = new Distro(globals.Bins);

                Distro.Prepare(
                    globals,
                    equityChanges1, bondChanges1, billChanges1,
                    distroEquities1, distroBonds1, distroBills1,
                    printlock);

                lock (printlock)
                {
                    Console.WriteLine(Utils.ResultHeader);
                }

                var res2 = Parallel.ForEach(
                    models,
                    (m) =>
                    {
                        var res1 = Parallel.ForEach(
                            sweeps,
                            (sw) =>
                            {
                                Model mm = Model.SweepModel(m, sw, c);
                                if (mm.Validate())
                                {
                                    if (mm.StartEq + mm.StartBo <= 100)
                                    {
                                        List<SingleRunResult> result = Models.RunDoublePortfolioExperiment(
                                            globals,
                                            mm,
                                            (double)globals.DoubleWorldWeight / (double)(globals.DoubleWorldWeight + globals.DoubleCountryWeight),
                                            distroEquities1, distroBonds1, distroBills1,
                                            distroEquities2, distroBonds2, distroBills2);

                                        ModelResult mr = new ModelResult(globals, mm, result);
                                        modelResults.Add(mr);
                                        Utils.WriteResult(null, m, mr, printlock);
                                    }
                                }
                            });
                    });

                c.Weight = 0;
            }
        }
    }
}
