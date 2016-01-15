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

    public class Model
    {
        public Model(
            int strategy, 
            double par1,
            double par2,
            double par3, 
            int cycles, 
            int repeats,
            int startSum, 
            int startEq, 
            int startBo,
            double yearlyWithdrawal,
            int rebalanceEvery,
            string comment)
        {
            Strategy = strategy;
            StrategyParameter1 = par1;
            StrategyParameter2 = par2;
            StrategyParameter3 = par3;
            Cycles = cycles;
            Repeats = repeats;
            StartSum = startSum;
            StartEq = startEq;
            StartBo = startBo;
            YearlyWithdrawal = yearlyWithdrawal;
            RebalanceEvery = rebalanceEvery;
            Comment = comment;
        }
        public int Strategy { get; set; }
        public double StrategyParameter1 { get; set; }
        public double StrategyParameter2 { get; set; }
        public double StrategyParameter3 { get; set; }
        public int Cycles { get; set; }
        public int Repeats{ get; set; }
        public int StartSum { get; set; }
        public int StartEq{ get; set; }
        public int StartBo{ get; set; }
        public double YearlyWithdrawal { get; set; }
        public int RebalanceEvery { get; set; }
        public string Comment { get; set; }
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
                    int.Parse(values[0]),
                    double.Parse(values[1]),
                    double.Parse(values[2]),
                    double.Parse(values[3]),
                    int.Parse(values[4]),
                    int.Parse(values[5]),
                    int.Parse(values[6]),
                    int.Parse(values[7]),
                    int.Parse(values[8]),
                    double.Parse(values[9]),
                    int.Parse(values[10]),
                    values[11]));
            }

            return list;
        }
    }



}