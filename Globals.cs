using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
    public class Globals
    {
        private static Globals s_globals = null;
        public string ResultLocation { get; set; }
        public string Prefix { get; set; }
        public int Cycles { get; set; }
        public int Repeats { get; set; }
        public int StartSum { get; set; }
        public int DoubleRebalance { get; set; }
        public int DoubleCountryWeight { get; set; }
        public int DoubleWorldWeight { get; set; }
        public string DoubleWorldName { get; set; }
        public int CutoffPercent { get; set; }
        public int EssentialsPercent { get; set; }
        public int AllowedInsufficientRate { get; set; }
        public int Bins { get; set; }
        public int WDBins { get; set; }
        public double StepsInYear { get; set; }
        public Double[] SweepWithdrawalRates { get; set; }
        public int[] SweepWorldShares { get; set; }
        public int[] SweepEquities { get; set; }
        public int[] SweepBonds { get; set; }
        public int[] SweepStrategies { get; set; }

        public Globals(
            int cycles,
            int repeats,
            int startsize,
            int doublerebalance,
            int doubleCountryWeight,
            int doubleWorldWeight,
            string doubleWorldName,
            int bins,
            int wdBins,
            int cutoff,
            int essentialsPercent,
            int allowedInsufficientRate,
            double stepsInYear,
            string prefix,
            string resultLocation,
            double[] sweepWithdrawalRates,
            int[] sweepWorldWeights,
            int[] sweepEquities,
            int[] sweepBonds,
            int[] sweepStrategies)
        {
            this.Cycles = cycles;
            this.Repeats = repeats;
            this.StartSum = startsize;
            this.DoubleRebalance = doublerebalance;
            this.DoubleCountryWeight = doubleCountryWeight;
            this.DoubleWorldWeight = doubleWorldWeight;
            this.DoubleWorldName = doubleWorldName;
            this.Bins = bins;
            this.WDBins = wdBins;
            this.StepsInYear = stepsInYear;
            this.CutoffPercent = cutoff;
            this.Prefix = prefix;
            this.ResultLocation = resultLocation;
            this.SweepWithdrawalRates = sweepWithdrawalRates;
            this.SweepWorldShares = sweepWorldWeights;
            this.SweepEquities = sweepEquities;
            this.SweepBonds = sweepBonds;
            this.SweepStrategies = sweepStrategies;
            this.EssentialsPercent = essentialsPercent;
            this.AllowedInsufficientRate = allowedInsufficientRate;
        }

        public static void ReadParams(string fname)
        {
            int cycles = 400;
            int repeats = 1000;
            int startsize = 4000000;

            int doublerebalance = cycles;
            int doubleCountryWeight = 1;
            int doubleWorldWeight = 1;
            double stepsInYear = 10.8684;
            int bins = 200;
            int wdBins = 5;
            int cutoff = 95;
            int essentialsPercent = 80;
            int allowedInsufficientRate = 5;
            string doubleWorldName = "world.jpg";
            Double[] sweepWithdrawalRates = { 0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5, 5.5, 6, 6.5, 7, 7.5, 8, 8.5, 9, 9.5 };
            int[] sweepWorldWeights = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            int[] sweepEquities = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            int[] sweepBonds = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            int[] sweepStrategies = { 1, 2, 3 };

            string resultLocation = ".";
            string prefix = "R";

            var sr = new StreamReader(File.OpenRead(fname));
            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine().Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                var values = line.Split(',');
                switch (values[0].ToLower().Trim())
                {
                    case "prefix":
                        prefix = values[1];
                        break;
                    case "location":
                        resultLocation = values[1];
                        break;
                    case "cycles":
                        cycles = int.Parse(values[1]);
                        break;
                    case "repeats":
                        repeats = int.Parse(values[1]);
                        break;
                    case "startsum":
                        startsize = int.Parse(values[1]);
                        break;
                    case "doubleworldname":
                        doubleWorldName = values[1];
                        break;
                    case "doublerebalance":
                        doublerebalance = int.Parse(values[1]);
                        break;
                    case "doubleworldweight":
                        doubleWorldWeight = int.Parse(values[1]);
                        break;
                    case "doublecountryweight":
                        doubleCountryWeight = int.Parse(values[1]);
                        break;
                    case "bins":
                        bins = int.Parse(values[1]);
                        break;
                    case "wdbins":
                        wdBins = int.Parse(values[1]);
                        break;
                    case "cutoff":
                        cutoff = int.Parse(values[1]);
                        break;
                    case "essentialspercent":
                        essentialsPercent = int.Parse(values[1]);
                        break;
                    case "allowedinsufficientrate":
                        allowedInsufficientRate = int.Parse(values[1]);
                        break;
                    case "stepsinyear":
                        stepsInYear = double.Parse(values[1]);
                        break;
                    case "sweepwithdrawalrates":
                        sweepWithdrawalRates = new double[values.Length - 2];
                        for (int i = 1; i < values.Length - 1; i++)
                            sweepWithdrawalRates[i - 1] = double.Parse(values[i]);
                        break;
                    case "sweepworld":
                        sweepWorldWeights = new int[values.Length - 2];
                        for (int i = 1; i < values.Length - 1; i++)
                            sweepWorldWeights[i - 1] = int.Parse(values[i]);
                        break;
                    case "sweepequities":
                        sweepEquities = new int[values.Length - 2];
                        for (int i = 1; i < values.Length - 1; i++)
                            sweepEquities[i - 1] = int.Parse(values[i]);
                        break;
                    case "sweepbonds":
                        sweepBonds = new int[values.Length - 2];
                        for (int i = 1; i < values.Length - 1; i++)
                            sweepBonds[i - 1] = int.Parse(values[i]);
                        break;
                    case "sweepstr":
                        sweepStrategies = new int[values.Length - 2];
                        for (int i = 1; i < values.Length - 1; i++)
                            sweepStrategies[i - 1] = int.Parse(values[i]);
                        break;
                }
            }

            s_globals = new Globals(
                cycles,
                repeats,
                startsize,
                doublerebalance,
                doubleCountryWeight,
                doubleWorldWeight,
                doubleWorldName,
                bins,
                wdBins,
                cutoff,
                essentialsPercent,
                allowedInsufficientRate,
                stepsInYear,
                prefix,
                resultLocation,
                sweepWithdrawalRates,
                sweepWorldWeights,
                sweepEquities,
                sweepBonds,
                sweepStrategies);
        }

        public static Globals Singleton()
        {
            return s_globals;
        }
    }
}