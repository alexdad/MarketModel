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
    class Experiments
    {
        // One run for a single portfolio
        public static void ExecuteSingle(
            List<Country> countries,
            List<Model> models,
            ConcurrentBag<ModelResult> modelResults,
            Object printlock)
        {
            List<int> equityChanges = new List<int>();
            List<int> bondChanges = new List<int>();
            List<int> billChanges = new List<int>();

            GraphAcquierer.Acquire(countries, equityChanges, bondChanges, billChanges, printlock);

            Distro distroEquities = new Distro(Globals.Singleton().Bins);
            Distro distroBonds = new Distro(Globals.Singleton().Bins);
            Distro distroBills = new Distro(Globals.Singleton().Bins);

            Distro.Prepare(
                equityChanges, bondChanges, billChanges,
                distroEquities, distroBonds, distroBills,
                printlock);

            Distro.Test(
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
                        List<SingleRunResult> result = Market.RunSinglePortfolioExperiment(
                            m,
                            distroEquities, distroBonds, distroBills);

                        ModelResult mr = new ModelResult(m, result);
                        modelResults.Add(mr);
                        Utils.WriteResult(null, m, mr, printlock);
                    }
                });
        }

        // One run for a 2-part portfolio
        public static void ExecuteDouble(
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
                if (Globals.IsWorld(c.Filename))
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

            Distro distroEquities2 = new Distro(Globals.Singleton().Bins);
            Distro distroBonds2 = new Distro(Globals.Singleton().Bins);
            Distro distroBills2 = new Distro(Globals.Singleton().Bins);

            Distro.Prepare(
                equityChanges2, bondChanges2, billChanges2,
                distroEquities2, distroBonds2, distroBills2,
                printlock);

            // Group1 is all except World
            List<int> equityChanges1 = new List<int>();
            List<int> bondChanges1 = new List<int>();
            List<int> billChanges1 = new List<int>();

            GraphAcquierer.Acquire(countries1, equityChanges1, bondChanges1, billChanges1, printlock);

            Distro distroEquities1 = new Distro(Globals.Singleton().Bins);
            Distro distroBonds1 = new Distro(Globals.Singleton().Bins);
            Distro distroBills1 = new Distro(Globals.Singleton().Bins);

            Distro.Prepare(
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
                        List<SingleRunResult> result = Market.RunDoublePortfolioExperiment(
                            m,
                            weight2 / (weight1 + weight2),
                            distroEquities1, distroBonds1, distroBills1,
                            distroEquities2, distroBonds2, distroBills2);

                        ModelResult mr = new ModelResult(m, result);
                        modelResults.Add(mr);
                        Utils.WriteResult(null, m, mr, printlock);

                    }
                });
        }

        // Sweep run for a single portfolio
        public static void ExecuteSweepSingle(
            List<Country> countries,
            List<Model> models,
            SweepParameters[] sweeps,
            ConcurrentBag<ModelResult> modelResults,
            Object printlock)
        {
            Country c = new Country("", 0, 0, 0.0, 0.0, 0.0, 0.0, 0);

            List<int> equityChanges = new List<int>();
            List<int> bondChanges = new List<int>();
            List<int> billChanges = new List<int>();

            GraphAcquierer.Acquire(countries, equityChanges, bondChanges, billChanges, printlock);

            Distro distroEquities = new Distro(Globals.Singleton().Bins);
            Distro distroBonds = new Distro(Globals.Singleton().Bins);
            Distro distroBills = new Distro(Globals.Singleton().Bins);

            Distro.Prepare(
                equityChanges, bondChanges, billChanges,
                distroEquities, distroBonds, distroBills,
                printlock);

            Distro.Test(
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
                                List<SingleRunResult> result = Market.RunSinglePortfolioExperiment(
                                    mm,
                                    distroEquities, distroBonds, distroBills);

                                ModelResult mr = new ModelResult(mm, result);
                                modelResults.Add(mr);
                                Utils.WriteResult(null, mm, mr, printlock);
                            }
                        });
                });
        }

        // Sweep run for a double-part portfolio 
        public static void ExecuteSweepDouble(
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
                if (Globals.IsWorld(c.Filename))
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

            Distro distroEquities2 = new Distro(Globals.Singleton().Bins);
            Distro distroBonds2 = new Distro(Globals.Singleton().Bins);
            Distro distroBills2 = new Distro(Globals.Singleton().Bins);

            Distro.Prepare(
                equityChanges2, bondChanges2, billChanges2,
                distroEquities2, distroBonds2, distroBills2,
                printlock);

            // Group1 is all except World
            List<int> equityChanges1 = new List<int>();
            List<int> bondChanges1 = new List<int>();
            List<int> billChanges1 = new List<int>();

            GraphAcquierer.Acquire(countries1, equityChanges1, bondChanges1, billChanges1, printlock);

            Distro distroEquities1 = new Distro(Globals.Singleton().Bins);
            Distro distroBonds1 = new Distro(Globals.Singleton().Bins);
            Distro distroBills1 = new Distro(Globals.Singleton().Bins);

            Distro.Prepare(
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
                                List<SingleRunResult> result = Market.RunDoublePortfolioExperiment(
                                    mm,
                                    weight2 / (weight1 + weight2),
                                    distroEquities1, distroBonds1, distroBills1,
                                    distroEquities2, distroBonds2, distroBills2);

                                ModelResult mr = new ModelResult(mm, result);
                                modelResults.Add(mr);
                                Utils.WriteResult(null, mm, mr, printlock);
                            }
                        });
                });
        }

        // Sweep run for a single portfolio by country
        public static void ExecuteSweepSingleByCountry(
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

                Distro distroEquities = new Distro(Globals.Singleton().Bins);
                Distro distroBonds = new Distro(Globals.Singleton().Bins);
                Distro distroBills = new Distro(Globals.Singleton().Bins);

                Distro.Prepare(
                    equityChanges, bondChanges, billChanges,
                    distroEquities, distroBonds, distroBills,
                    printlock);

                Distro.Test(
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
                                        List<SingleRunResult> result = Market.RunSinglePortfolioExperiment(
                                            mm,
                                            distroEquities, distroBonds, distroBills);

                                        ModelResult mr = new ModelResult(mm, result);
                                        modelResults.Add(mr);
                                        Utils.WriteResult(null, mm, mr, printlock);
                                    }
                                }
                            });
                    });

                c.Weight = 0;
            }
        }

        // Sweep run for a double-part portfolio  by country
        public static void ExecuteSweepDoubleByCountry(
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
                if (Globals.IsWorld(c.Filename))
                {
                    countries2.Add(c);
                    countries2.Last().Weight = 1;
                }
            }

            List<int> equityChanges2 = new List<int>();
            List<int> bondChanges2 = new List<int>();
            List<int> billChanges2 = new List<int>();

            GraphAcquierer.Acquire(countries2, equityChanges2, bondChanges2, billChanges2, printlock);

            Distro distroEquities2 = new Distro(Globals.Singleton().Bins);
            Distro distroBonds2 = new Distro(Globals.Singleton().Bins);
            Distro distroBills2 = new Distro(Globals.Singleton().Bins);

            Distro.Prepare(
                equityChanges2, bondChanges2, billChanges2,
                distroEquities2, distroBonds2, distroBills2,
                printlock);

            // Now enumerate countries; Group1 each time will carry just one
            foreach (var c in countries)
            {
                if (Globals.IsWorld(c.Filename))
                    continue;
                List<Country> countries1 = new List<Country>();
                countries1.Add(c);
                countries1.Last().Weight = 1;

                // Group1 is just one country
                List<int> equityChanges1 = new List<int>();
                List<int> bondChanges1 = new List<int>();
                List<int> billChanges1 = new List<int>();

                GraphAcquierer.Acquire(countries1, equityChanges1, bondChanges1, billChanges1, printlock);

                Distro distroEquities1 = new Distro(Globals.Singleton().Bins);
                Distro distroBonds1 = new Distro(Globals.Singleton().Bins);
                Distro distroBills1 = new Distro(Globals.Singleton().Bins);

                Distro.Prepare(
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
                                        List<SingleRunResult> result = Market.RunDoublePortfolioExperiment(
                                            mm,
                                            sw.WorldShare,
                                            distroEquities1, distroBonds1, distroBills1,
                                            distroEquities2, distroBonds2, distroBills2);

                                        ModelResult mr = new ModelResult(mm, result);
                                        modelResults.Add(mr);
                                        Utils.WriteResult(null, mm, mr, printlock);
                                    }
                                }
                            });
                    });

                c.Weight = 0;
            }
        }
    }
}