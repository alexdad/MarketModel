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
            string globalFileName, modelsFilename, countriesFileName;
            Portfolio portfolio = Portfolio.Single;
            SweepMode sweepMode = SweepMode.No;
            SweepParameters[] sweeps = new SweepParameters[1];
            int nFactors = 0;
            const int maxFactors = 10;
            Factor[] factors = new Factor[maxFactors];
            int cp = 0;

            for (int f = 0; f < factors.Length; f++)
                factors[f] = Factor.None;

            // Global params file
            if (args.Length <= cp)
            {
                Console.Write("Usage: <globals.csv> <countries.csv> <models.csv> [single|dual] [sweep N factor-1 ... factor-n]");
                return;
            }

            globalFileName = args[cp++];
            Globals.ReadParams(globalFileName);
            string resultPrefix = "R_" + Globals.Singleton().Prefix;

            // Countries file
            if (args.Length <= cp)
            {
                Console.Write("Second parameter must point to the countries file");
                return;
            }
            countriesFileName = args[cp++];
            List<Country> countries = Country.ReadCountries(countriesFileName, false);

            // Models file
            if (args.Length <= cp)
            {
                Console.Write("Third parameter must point to the models file");
                return;
            }
            modelsFilename = args[cp++];
            List<Model> models = Model.ReadModels(modelsFilename);

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
                        case "str":
                            factors[i] = Factor.Strategy;
                            resultPrefix += "_Strategy";
                            break;
                        case "world":
                            factors[i] = Factor.WorldShare;
                            resultPrefix += "_World";
                            break;
                        case "wd":
                            factors[i] = Factor.Withdrawal;
                            resultPrefix += "_Withdrawal";
                            break;
                        case "eq":
                            factors[i] = Factor.Equity;
                            resultPrefix += "_Equity";
                            break;
                        case "bo":
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
                sweeps = Utils.Factorize(factors, countries);
                Console.WriteLine("You requested to sweep across {0} combinations", sweeps.Length);
            }

            // Create results dir and copy controling files
            Utils.CreateResultDir(resultPrefix, globalFileName, countriesFileName, modelsFilename);

            Utils.SaveCommand(Utils.CommandFileName(resultPrefix), args);

            // Run simulations
            Execute(
                countries,
                models,
                portfolio,
                sweepMode,
                sweeps,
                Utils.ResultFileName(resultPrefix),
                Utils.SummaryFileName(resultPrefix));
        }

        static void Execute(
            List<Country> countries,
            List<Model> models,
            Portfolio portfolio,
            SweepMode sweepMode,
            SweepParameters[] sweeps,
            string resFile,
            string summaryFile)
        {
            ConcurrentBag<ModelResult> modelResults = new ConcurrentBag<ModelResult>();
            Object printLock = new Object();

            if (sweepMode == SweepMode.No)
            {
                if (portfolio == Portfolio.Single)
                {
                    Experiments.ExecuteSingle(countries, models, modelResults, printLock);
                }
                else if (portfolio == Portfolio.Double)
                {
                    Experiments.ExecuteDouble(countries, models, modelResults, printLock);
                }
            }
            else if (sweepMode == SweepMode.SweepNoCountry)
            {
                if (portfolio == Portfolio.Single)
                {
                    Experiments.ExecuteSweepSingle(countries, models, sweeps, modelResults, printLock);
                }
                else if (portfolio == Portfolio.Double)
                {
                    Experiments.ExecuteSweepDouble(countries, models, sweeps, modelResults, printLock);
                }
            }
            else if (sweepMode == SweepMode.SweepAndCountry)
            {
                if (portfolio == Portfolio.Single)
                {
                    Experiments.ExecuteSweepSingleByCountry(countries, models, sweeps, modelResults, printLock);
                }
                else if (portfolio == Portfolio.Double)
                {
                    Experiments.ExecuteSweepDoubleByCountry(countries, models, sweeps, modelResults, printLock);
                }
            }

            Analysis.Analyze(
                            countries,
                            models,
                            portfolio,
                            sweepMode,
                            sweeps,
                            modelResults, 
                            resFile, 
                            summaryFile, 
                            printLock);
        }
    }
}
