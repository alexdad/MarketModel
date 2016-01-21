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
    class Analysis
    {
        public static void Analyze(
            List<Country> countries,
            List<Model> models,
            Portfolio portfolio,
            SweepMode sweepMode,
            SweepParameters[] sweeps,
            ConcurrentBag<ModelResult> modelResults,
            string resFile,
            string summaryFile,
            Object printLock)
        {
            Dictionary<SweepParameters, SweepStat> sorter = new Dictionary<SweepParameters, SweepStat>();

            foreach (ModelResult mr in modelResults)
            {
                SweepParameters swp = new SweepParameters();
                swp.Country = -1;
                swp.WithdrawalRate = mr.model.YearlyWithdrawal;
                swp.WorldShare = mr.model.WorldShare;
                swp.Equity = mr.model.StartEq;
                swp.Bonds = mr.model.StartBo;
                swp.Strategy = mr.model.Strategy;
                int countryIndex = 0;
                foreach(Country c in countries)
                {
                    if (c.Filename == mr.model.CountryName)
                        break;
                    else
                        countryIndex++;
                }

                if (!sorter.ContainsKey(swp))
                {
                    SweepStat sws = new SweepStat();
                    sws.sweepResults = new Dictionary<int, List<ModelResult>>();
                    sorter.Add(swp, sws);
                }

                if (!sorter[swp].sweepResults.ContainsKey(countryIndex))
                    sorter[swp].sweepResults.Add(countryIndex, new List<ModelResult>());

                sorter[swp].sweepResults[countryIndex].Add(mr);
            }

            List<SweepResult> sweepResults = new List<SweepResult>();

            foreach(SweepParameters swp in sorter.Keys)
            {
                SweepStat sws = sorter[swp];
                foreach (int country in sorter[swp].sweepResults.Keys)
                {
                    foreach (ModelResult mr in sorter[swp].sweepResults[country])
                    {
                        sws.totalPop += countries[country].Population;
                        sws.weightedProd += mr.productivity * countries[country].Population;
                        sws.weightedSuccessRate += mr.overallSuccessRate * countries[country].Population;
                    }
                }
                sws.weightedProd /= sws.totalPop;
                sws.weightedSuccessRate /= sws.totalPop;
                
                SweepResult swr = new SweepResult();
                swr.parameters = swp;
                swr.stat = sws;
                sweepResults.Add(swr);
            }
            
            IEnumerable<SweepResult> sortedResults = sweepResults.OrderBy(
                sr => (sr.stat.weightedSuccessRate * 10000.0 + sr.stat.weightedProd));

            Console.WriteLine("WD,Stream,World,Eq,Bo,Prod,Success,");
            foreach(SweepResult  sr in sortedResults)
            {
                Console.WriteLine("{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},", 
                    sr.parameters.WithdrawalRate,
                    sr.parameters.Strategy,
                    sr.parameters.WorldShare,
                    sr.parameters.Equity,
                    sr.parameters.Bonds,
                    sr.stat.weightedProd,
                    sr.stat.weightedSuccessRate);

            }

            // Simple sorting of successful runs
            SimpeSort(modelResults, resFile, summaryFile, printLock);
        }
        public static void SimpeSort(
            ConcurrentBag<ModelResult> modelResults,
            string resFile,
            string summaryFile,
            Object printLock)
        {
            IEnumerable<ModelResult> sortedResults = modelResults.OrderBy(
                mr => (mr.productivity));

            Dictionary<string, double> cutoffWdRequested = new Dictionary<string, double>();
            Dictionary<string, double> cutoffProd = new Dictionary<string, double>();

            using (StreamWriter sw = new StreamWriter(resFile))
            {
                sw.WriteLine(Utils.ResultHeader);

                foreach (ModelResult mr in sortedResults)
                {
                    Utils.WriteResult(sw, mr, printLock);

                    if (mr.overallSuccessRate >= Globals.Singleton().CutoffPercent / 100.0)
                    {
                        if (!cutoffProd.ContainsKey(mr.model.CountryName) ||
                            mr.productivity >= cutoffProd[mr.model.CountryName])
                        {
                            cutoffProd[mr.model.CountryName] = mr.productivity;
                            cutoffWdRequested[mr.model.CountryName] = mr.model.YearlyWithdrawal;
                        }
                    }
                    else if (!cutoffProd.ContainsKey(mr.model.CountryName))
                    {
                        cutoffProd[mr.model.CountryName] = -1;
                        cutoffWdRequested[mr.model.CountryName] = -1;
                    }
                }
            }

            using (StreamWriter sw = new StreamWriter(summaryFile))
            {
                foreach (string c in cutoffWdRequested.Keys)
                {
                    sw.WriteLine("{0},{1:F2},{2:F1},", c, cutoffProd[c], cutoffWdRequested[c]);
                }
            }
        }
    }
}