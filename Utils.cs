using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
    public enum Portfolio          // How is the portfolio constructed?  
    {
        Single,             // Portfolio consists of a single mix of equity/bonds/bills
        Double              // Portfolio consists of 2 parts, each with its own mix of equity/bonds/bills
        //    Part 1 = same as Single, but without a World (last entry in countries.csv)
        //    Part 2 = World (last entry in countries.csv
    };

    public enum RunMode            // How many series do we run? 
    {
        Single,             // We run just one series of simulations
        Sweep               // We run one series per each combination of sweep parameters
    };

    public enum SweepMode         // Which parameters to seep upon?
    {
        No,                 // no sweep at all
        SweepNoCountry,     // Sweep on some parameters, but not on country
        SweepAndCountry     // Sweep on some parameters, including country
    };

    public enum Factor
    {
        Strategy,           // Try every strategy (1..3 for now)
        Withdrawal,         // Try every withdrawal rate of the predefined set
        WorldShare,         // Try every world component share of the predefined set
        Equity,             // Try every equity share of the predefined set
        Bonds,              // Try every bonds share of the predefined set
        None,
    };

    public struct SweepParameters
    {
        public int Country;
        public double WithdrawalRate;
        public int WorldShare;
        public int Equity;
        public int Bonds;
        public int Strategy;
    }

    public class Params
    {
        public static int PercentageScale { get { return 10000; } }
        public static int Bins { get { return 200; } }
        public static double StepsInYear { get { return 1239.0 / 114.0; } }

        public static string ResultFileName(string prefix)
        {
            return String.Format("{0}-{1}{2}{3}-{4}{5}{6}.csv",
                prefix, 
                DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
        }

        public static SweepParameters[] Factorize(Factor[] factors, List<Country> countries)
        {
            int[] SweepStrategies = { 1, 2, 3 };
            Double[] SweepWithdrawalRates = { 0, 0.5, 1, 1.5, 2, 2.5, 2, 3.5, 4, 4.5, 6, 6.5, 7, 7.5, 8, 8.5, 9, 9.5 };
            int[] SweepWorldShares = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            int[] SweepEquities = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            int[] SweepBonds = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            SweepParameters[] sweeps = new SweepParameters[1];
            sweeps[0].Strategy = -1;
            sweeps[0].Equity = -1;
            sweeps[0].Bonds = -1;
            sweeps[0].WorldShare = -1;
            sweeps[0].WithdrawalRate = -1;
            sweeps[0].Country = -1;

            int nCountries = countries.Count;

            for (int f = 0; f < factors.Length; f++)
            {
                switch (factors[f])
                {
                    case Factor.Strategy:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * SweepStrategies.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < SweepStrategies.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].Strategy = SweepStrategies[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.Withdrawal:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * SweepWithdrawalRates.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < SweepWithdrawalRates.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].WithdrawalRate = SweepWithdrawalRates[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.WorldShare:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * SweepWorldShares.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < SweepWorldShares.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].WorldShare = SweepWorldShares[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.Equity:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * SweepEquities.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < SweepEquities.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].Equity = SweepEquities[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.Bonds:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * SweepBonds.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < SweepBonds.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].Bonds = SweepBonds[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.None:
                        break;
                }
            }

            List<SweepParameters> sweeps1 = new List<SweepParameters>();
            int count = 0;
            for (int i = 0; i < sweeps.Length; i++)
            {
                if (sweeps[i].Equity + sweeps[i].Bonds <= 100)
                {
                    sweeps1.Add(sweeps[i]);
                    count++;
                }
            }

            sweeps = new SweepParameters[count];
            int cn = 0;
            foreach (SweepParameters s in sweeps1)
                sweeps[cn++] = s;

            return sweeps;
        }
    }

    public class Country
    {
        public Country(string fname, int bp, int tp, float le, float lbo, float lbi, int weight)
        {
            Filename = fname;
            BottomPower = bp;
            TopPower = tp;
            LastEquity = le;
            LastBond = lbo;
            LastBill = lbi;
            Weight = weight;
        }
        public string Filename { get; set; }
        public int BottomPower { get; set; }
        public int TopPower { get; set; }
        public double LastEquity { get; set; }
        public double LastBond { get; set; }
        public double LastBill { get; set; }
        public int Weight { get; set; }

        public static List<Country> ReadCountries(string fname, bool ignoreZeroWeights)
        {
            var sr = new StreamReader(File.OpenRead(fname));
            List<Country> list = new List<Country>();
            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine().Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                var values = line.Split(',');
                int weight = int.Parse(values[6]);
                if (weight > 0)
                {
                    list.Add(new Country(
                        values[0],
                        int.Parse(values[1]),
                        int.Parse(values[2]),
                        float.Parse(values[3]),
                        float.Parse(values[4]),
                        float.Parse(values[5]),
                        weight));
                }
            }

            return list;
        }
    }

    public class GlobalParams
    {
        public GlobalParams(
            int cycles, 
            int repeats, 
            int startsize, 
            int doublerebalance,
            double doubleWorldWeight,
            string doubleWorldName,
            string prefix)
        {
            this.Cycles = cycles;
            this.Repeats = repeats;
            this.StartSum = startsize;
            this.DoubleRebalance = doublerebalance;
            this.DoubleWorldWeight = doubleWorldWeight;
            this.DoubleWorldName = doubleWorldName;
            this.Prefix = prefix;
        }

        public int Cycles { get; set; }
        public int Repeats { get; set; }
        public int StartSum { get; set; }
        public int DoubleRebalance { get; set; }
        public double DoubleWorldWeight { get; set; }
        public string DoubleWorldName { get; set; }
        public string Prefix { get; set; }

        public static GlobalParams ReadParams(string fname)
        {
            int cycles = 400;
            int repeats = 1000;
            int startsize = 4000000;

            int    doublerebalance = cycles;
            double doubleWorldWeight = 1.0;
            string doubleWorldName = "world.jpg";
            
            string prefix = "R";

            var sr = new StreamReader(File.OpenRead(fname));
            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine().Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                var values = line.Split(',');
                switch(values[0].ToLower())
                {
                    case "prefix":
                        prefix = values[1];
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
                    case "doublerebalance":
                        doublerebalance = int.Parse(values[1]);
                        break;
                    case "doubleworldweight":
                        doubleWorldWeight = double.Parse(values[1]);
                        break;
                    case "doubleWorldName":
                        doubleWorldName = values[1];
                        break;
                }
            }

            return new GlobalParams(
                cycles,
                repeats,
                startsize,
                doublerebalance,
                doubleWorldWeight,
                doubleWorldName,
                prefix);
                    
        }

    }
    public class Model
    {
        public Model(
            int strategy, 
            int startEq, 
            int startBo,
            double yearlyWithdrawal,
            int rebalanceEvery)
        {
            Strategy = strategy;
            StartEq = startEq;
            StartBo = startBo;
            YearlyWithdrawal = yearlyWithdrawal;
            RebalanceEvery = rebalanceEvery;
        }
        public int Strategy { get; set; }
        public int StartEq{ get; set; }
        public int StartBo{ get; set; }
        public double YearlyWithdrawal { get; set; }
        public int RebalanceEvery { get; set; }
        public static List<Model> ReadModels(string fname)
        {
            var sr = new StreamReader(File.OpenRead(fname));
            List<Model> list = new List<Model>();
            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine().Trim();
                if (line.Length == 0 || !Char.IsDigit(line[0]))
                    continue;
                var values = line.Split(',');
                list.Add(new Model(
                    int.Parse(values[0]),      // strategy
                    int.Parse(values[1]),      // eq
                    int.Parse(values[2]),      // bo
                    double.Parse(values[3]),   // wthdr
                    int.Parse(values[4])));    // rebal 
            }

            return list;
        }

        public static Model SweepModel(Model mp, SweepParameters sw)
        {
            Model m = new Model(mp.Strategy, mp.StartEq, mp.StartBo, mp.YearlyWithdrawal, mp.RebalanceEvery);
            if (sw.Strategy >= 0)
                m.Strategy = sw.Strategy;
            if (sw.Equity >= 0)
                m.StartEq = sw.Equity;
            if (sw.Bonds >= 0)
                m.StartBo = sw.Bonds;
            if (sw.WithdrawalRate >= 0)
                m.YearlyWithdrawal = sw.WithdrawalRate;

            return m;
        }
    }


    public class ModelResult
    {
        public ModelResult(Model m, List<SingleRunResult> results)
        {
            this.model = m;

            int failures = 0, successes = 0;
            double successRate = Models.Check(results, ref failures, ref successes);
            
            trailSuccessRate = successRate;

            trailAverage = withdrawalAverage = 0;
            trailMin = withdrawalMin = double.MaxValue;
            trailMax = withdrawalMax = double.MinValue;

            int count = 0;
            foreach(var sr in results)
            {
                trailAverage += sr.TrailingAmount;
                trailMax = Math.Max(trailMax, sr.TrailingAmount);
                trailMin = Math.Min(trailMin, sr.TrailingAmount);
                withdrawalAverage += sr.WithdrawalAver;
                withdrawalMax = Math.Max(withdrawalMax, sr.WithdrawalMax);
                withdrawalMin = Math.Min(withdrawalMin, sr.WithdrawalMin);
                count++;
            }

            this.trailAverage /= (count * 1000000.0);
            this.withdrawalAverage /= count;
            this.trailMax = trailMax / 1000000.0;
            this.trailMin = trailMin / 1000000.0;
        }

        public Model model;
        public double trailSuccessRate;
        public double trailAverage;
        public double trailMin;
        public double trailMax;
        public double withdrawalAverage;
        public double withdrawalMin;
        public double withdrawalMax;
    }

    public class SingleRunResult
    {
        public SingleRunResult(double trailingAmount, double withdrawalAver, double withdrawalMin, double withdrawalMax)
        {
            this.TrailingAmount = trailingAmount;
            this.WithdrawalAver = withdrawalAver;
            this.WithdrawalMax = withdrawalMax;
            this.WithdrawalMin = withdrawalMin;
        }

        public double TrailingAmount { get; set; }
        public double WithdrawalAver { get; set; }
        public double WithdrawalMin { get; set; }
        public double WithdrawalMax { get; set; }
    }
}