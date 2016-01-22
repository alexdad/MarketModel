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
            string perCountrySummaryFilename,
            string crossCountrySummaryFilename,
            Object printLock)
        {
            // Simple sorting of successful runs
            PerCountryAnalysis(modelResults, perCountrySummaryFilename, printLock);

            if (sweepMode == SweepMode.SweepAndCountry)
            {
                CrossCountryAnalysis(countries, models, portfolio,
                                     sweepMode, sweeps, modelResults,
                                     perCountrySummaryFilename, crossCountrySummaryFilename, printLock);
            }

        }

        public static void CrossCountryAnalysis(
            List<Country> countries,
            List<Model> models,
            Portfolio portfolio,
            SweepMode sweepMode,
            SweepParameters[] sweeps,
            ConcurrentBag<ModelResult> modelResults,
            string perCountrySummaryFilename,
            string crossCountrySummaryFilename,
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

            using (StreamWriter sw = new StreamWriter(crossCountrySummaryFilename))
            {
                sw.WriteLine("WdRate,Strategy,World,Eq,Bo,Prod,Success,");
                foreach (SweepResult sr in sortedResults)
                {
                    sw.WriteLine("{0:F2},{1},{2:F2},{3},{4},{5:F2},{6:F2},",
                        sr.parameters.WithdrawalRate,
                        sr.parameters.Strategy,
                        sr.parameters.WorldShare,
                        sr.parameters.Equity,
                        sr.parameters.Bonds,
                        sr.stat.weightedProd,
                        sr.stat.weightedSuccessRate);

                }
            }

        }
        public static void PerCountryAnalysis(
            ConcurrentBag<ModelResult> modelResults,
            string summaryFile,
            Object printLock)
        {
            IEnumerable<ModelResult> sortedResults = modelResults.OrderBy(
                mr => (mr.productivity));

            Dictionary<string, double> bestReliableProd = new Dictionary<string, double>();
            Dictionary<string, Model> bestReliableModel = new Dictionary<string, Model>();

            foreach (ModelResult mr in sortedResults)
            {
                if (mr.overallSuccessRate >= Globals.Singleton().CutoffPercent / 100.0)
                {
                    if (!bestReliableProd.ContainsKey(mr.model.CountryName) ||
                        mr.productivity >= bestReliableProd[mr.model.CountryName])
                    {
                        bestReliableProd[mr.model.CountryName] = mr.productivity;
                        bestReliableModel[mr.model.CountryName] = mr.model;
                    }
                }
            }

            using (StreamWriter sw = new StreamWriter(summaryFile))
            {
                sw.WriteLine("Country,Strategy,Eq,Bo,WorldShare,WdRate,Best_Reliable_Productivity_Effective");
                foreach (string c in bestReliableModel.Keys)
                {
                    sw.WriteLine("{0},{1},{2},{3},{4:F2},{5:F2},{6:F2},", 
                        c,  
                        bestReliableModel[c].Strategy,
                        bestReliableModel[c].StartEq,
                        bestReliableModel[c].StartBo,
                        bestReliableModel[c].WorldShare,
                        bestReliableModel[c].YearlyWithdrawal,
                        bestReliableProd[c]);
                }
            }
        }
    }
}