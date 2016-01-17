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

    public class Utils
    {
        public static int PercentageScale { get { return 10000; } }
        public static double StepsInYear { get { return 1239.0 / 114.0; } }

        public const string ResultHeader = "Country,Strategy,Eq,Bo,Withdrawal,Rebalance,TrailAver,TrailMax,TrailMin,WDAver,WDMax,WDMin,Q1,Q2,Q3,Q4,Q5,TrailSuccess,WDSuccess,SuccessRate, ";

        public const string ResultFormat = "{0},{1},{2},{3},{4:F2},{5},{6:F2},{7:F2},{8:F2},{9:F0},{10:F0},{11:F0},{12}{13:F2},{14:F2},{15:F2},";

        public static void WriteResult(StreamWriter sw, ModelResult mr, object printlock)
        {
            lock (printlock)
            {
                Utils.WriteResult(sw,
                                    mr.model.CountryName,
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
                                    mr.WDistrib,
                                    mr.trailSuccessRate,
                                    mr.withdrawalSuccessRate,
                                    mr.overallSuccessRate);
            }
        }

        public static void WriteResult(StreamWriter sw, Model mm, ModelResult mr, object printlock)
        {
            lock (printlock)
            {
                Utils.WriteResult(sw,
                    mm.CountryName, mm.Strategy,
                    mm.StartEq, mm.StartBo,
                    mm.YearlyWithdrawal, mm.RebalanceEvery,
                    mr.trailAverage, mr.trailMax, mr.trailMin,
                    mr.withdrawalAverage, mr.withdrawalMax, mr.withdrawalMin,
                    mr.WDistrib,
                    mr.trailSuccessRate,
                    mr.withdrawalSuccessRate,
                    mr.overallSuccessRate);
            }
        }

        public static void WriteResult(
            StreamWriter sw,  
            string country, int strategy, 
            int eq, int bo, 
            double wd, int rebalance, 
            double trailAv, double trailMax, double trailMin, 
            double wdAv, double wdMax, double wdMin, 
            double[] WDistrib,
            double trailSuccess,
            double wdSuccess,
            double successRate)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < WDistrib.Length; i++)
                sb.Append(string.Format("{0:F2},", WDistrib[i]));

            if (sw == null)
            {
                Console.WriteLine(ResultFormat,
                                    country, strategy,
                                    eq, bo,
                                    wd, rebalance,
                                    trailAv, trailMax, trailMin,
                                    wdAv, wdMax, wdMin, 
                                    sb.ToString(),
                                    trailSuccess,
                                    wdSuccess,
                                    successRate);
            }
            else
            {
                sw.WriteLine(ResultFormat,
                                    country, strategy,
                                    eq, bo,
                                    wd, rebalance,
                                    trailAv, trailMax, trailMin,
                                    wdAv, wdMax, wdMin,
                                    sb.ToString(),
                                    trailSuccess,
                                    wdSuccess,
                                    successRate);
            };
        }
     
        public static string ResultFileName(string prefix)
        {
            return String.Format("{0}-{1}{2}{3}-{4}{5}{6}.csv",
                prefix, 
                DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
        }

        public static SweepParameters[] Factorize(GlobalParams globals, Factor[] factors, List<Country> countries)
        {
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
                            sweeps = new SweepParameters[oldSweeps.Length * globals.SweepStrategies.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < globals.SweepStrategies.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].Strategy = globals.SweepStrategies[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.Withdrawal:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * globals.SweepWithdrawalRates.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < globals.SweepWithdrawalRates.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].WithdrawalRate = globals.SweepWithdrawalRates[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.WorldShare:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * globals.SweepWorldShares.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < globals.SweepWorldShares.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].WorldShare = globals.SweepWorldShares[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.Equity:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * globals.SweepEquities.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < globals.SweepEquities.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].Equity = globals.SweepEquities[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.Bonds:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * globals.SweepBonds.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < globals.SweepBonds.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].Bonds = globals.SweepBonds[n];
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
        public Country(string fname, int bp, int tp, double le, double lbo, double lbi, int weight)
        {
            Filename = fname;
            BottomPower = bp;
            TopPower = tp;
            LastEquity = le;
            LastBond = lbo;
            LastBill = lbi;
            Weight = weight;
        }
        public Country()
        {
            Filename = "";
            BottomPower = -1;
            TopPower = -1;
            LastEquity = -1;
            LastBond = -1;
            LastBill = -1;
            Weight = -1;
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
            double[] sweepWithdrawalRates,
            int[] sweepWorldShares,
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
            this.SweepWithdrawalRates = sweepWithdrawalRates;
            this.SweepWorldShares = sweepWorldShares;
            this.SweepEquities = sweepEquities;
            this.SweepBonds = sweepBonds;
            this.SweepStrategies = sweepStrategies;
            this.EssentialsPercent = essentialsPercent;
            this.AllowedInsufficientRate = allowedInsufficientRate;
        }

        public int Cycles { get; set; }
        public int Repeats { get; set; }
        public int StartSum { get; set; }
        public int DoubleRebalance { get; set; }
        public int DoubleCountryWeight { get; set; }
        public int DoubleWorldWeight { get; set; }
        public string DoubleWorldName { get; set; }
        public string Prefix { get; set; }
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


        public static GlobalParams ReadParams(string fname)
        {
            int cycles = 400;
            int repeats = 1000;
            int startsize = 4000000;

            int    doublerebalance = cycles;
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
            int[] sweepWorldShares = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            int[] sweepEquities = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            int[] sweepBonds = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            int[] sweepStrategies = { 1, 2, 3 };

            
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
                        for (int i = 1; i < values.Length-1; i++ )
                            sweepWithdrawalRates[i-1] = double.Parse(values[i]);
                        break;
                    case "sweepworldshares":
                        sweepWorldShares = new int[values.Length - 2];
                        for (int i = 1; i < values.Length-1; i++ )
                            sweepWorldShares[i-1] = int.Parse(values[i]);
                        break;
                    case "sweepequities":
                        sweepEquities = new int[values.Length - 2];
                        for (int i = 1; i < values.Length-1; i++ )
                            sweepEquities[i-1] = int.Parse(values[i]);
                        break;
                    case "sweepbonds":
                        sweepBonds = new int[values.Length - 2];
                        for (int i = 1; i < values.Length-1; i++ )
                            sweepBonds[i-1] = int.Parse(values[i]);
                        break;
                    case "sweepstr":
                        sweepStrategies = new int[values.Length - 2];
                        for (int i = 1; i < values.Length-1; i++ )
                            sweepStrategies[i-1] = int.Parse(values[i]);
                        break;
                }
            }

            return new GlobalParams(
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
                sweepWithdrawalRates,
                sweepWorldShares,
                sweepEquities,
                sweepBonds,
                sweepStrategies);
        }

    }
    public class Model
    {
        public Model(
            int strategy, 
            int startEq, 
            int startBo,
            double yearlyWithdrawal,
            int rebalanceEvery,
            string countryName)
        {
            Strategy = strategy;
            StartEq = startEq;
            StartBo = startBo;
            YearlyWithdrawal = yearlyWithdrawal;
            RebalanceEvery = rebalanceEvery;
            CountryName = countryName;
        }
        public int Strategy { get; set; }
        public int StartEq{ get; set; }
        public int StartBo{ get; set; }
        public double YearlyWithdrawal { get; set; }
        public int RebalanceEvery { get; set; }
        public string CountryName { get; set; } 
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
                    int.Parse(values[4]),      // rebal 
                    ""));                      // country name
            }

            return list;
        }

        public static Model SweepModel(Model mp, SweepParameters sw, Country c)
        {
            Model m = new Model(mp.Strategy, mp.StartEq, mp.StartBo, mp.YearlyWithdrawal, mp.RebalanceEvery, c.Filename);
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

        public bool Validate()
        {
            if (this.StartEq < 0 || this.StartEq > 100 ||
                this.StartBo < 0 || this.StartBo > 100)
                return false;
            if (this.StartEq + this.StartBo > 100)
                return false;
            if (this.Strategy < 1 || this.Strategy > 3)
                return false;
            if (this.YearlyWithdrawal < 0 || this.YearlyWithdrawal > 100)
                return false;
            if (this.RebalanceEvery < 0)
                return false;

            return true;
        }
    }


    public class ModelResult
    {
        public ModelResult(GlobalParams globals, Model m, List<SingleRunResult> results)
        {
            this.model = m;
            this.WDistrib = new double[globals.WDBins];
            for (int i = 0; i < globals.WDBins; i++)
            {
                this.WDistrib[i] = 0;
                foreach (var r in results)
                    this.WDistrib[i] = this.WDistrib[i] + r.WDistrib[i];
                this.WDistrib[i] /= results.Count;
            }

            int failures = 0, successes = 0;
            trailSuccessRate = Models.CheckTrailingAmount(globals, results, ref failures, ref successes);

            withdrawalSuccessRate = Models.CheckWithdrawals(globals, results, ref failures, ref successes);
            trailAverage = withdrawalAverage = 0;
            trailMin = withdrawalMin = double.MaxValue;
            trailMax = withdrawalMax = double.MinValue;

            overallSuccessRate = Models.CheckOverall(globals, results, ref failures, ref successes);

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
            this.trailMax = trailMax / 1000000.0;
            this.trailMin = trailMin / 1000000.0;
            this.withdrawalAverage /= count;
            this.withdrawalAverage *= (Utils.StepsInYear / 1000.0);
            this.withdrawalMax *= (Utils.StepsInYear / 1000.0);
            this.withdrawalMin *= (Utils.StepsInYear / 1000.0);
        }

        public Model model;
        public double overallSuccessRate;
        public double trailSuccessRate;
        public double trailAverage;
        public double trailMin;
        public double trailMax;
        public double withdrawalSuccessRate;
        public double withdrawalAverage;
        public double withdrawalMin;
        public double withdrawalMax;
        public double[] WDistrib;
    }

    public class SingleRunResult
    {
        public SingleRunResult(GlobalParams globals, string country, Model m, 
            double trailingAmount, double[] withdrawals)
        {
            this.Country = country;
            this.TrailingAmount = trailingAmount;
            SetWD(globals, m, withdrawals);
        }

        public SingleRunResult(GlobalParams globals, string country, Model m, 
            double trailingAmount, double[] withdrawals1, double[] withdrawals2)
        {
            this.Country = country;
            this.TrailingAmount = trailingAmount;

            int len = withdrawals1.Length;
            if (len != withdrawals2.Length)
                throw new Exception("Wrong lens");
            double[] withdrawals = new double[len];
            for (int i = 0; i < len; i++)
                withdrawals[i] = withdrawals1[i] + withdrawals2[i];

            SetWD(globals, m, withdrawals);
        }

        private void SetWD(GlobalParams globals, Model m, double[] withdrawals)
        {
            double essentialWD = globals.EssentialsPercent / 100.0 * globals.StartSum * Models.NormativeStepWD(m);
            int nSmallishWD = 0;
            foreach(var w in withdrawals)
            {
                if (w < essentialWD)
                    nSmallishWD++;
            }
            this.InsufficientWdRrate = (double)nSmallishWD * 100.0 / (double)withdrawals.Length;

            double[] binCounts = new double[globals.WDBins];
            for (int i = 0; i < binCounts.Length; i++)
                binCounts[i] = 0.0;

            this.WithdrawalAver = withdrawals.Average();
            this.WithdrawalMax = withdrawals.Max();
            this.WithdrawalMin = withdrawals.Min();
            double binSize = (WithdrawalMax - WithdrawalMin) / globals.WDBins;
            double count = 0;
            if (WithdrawalMax - WithdrawalMin > 1000)
            {
                foreach (double wd in withdrawals)
                {
                    int ind = (int)((wd - this.WithdrawalMin - 1) / binSize);
                    binCounts[ind] = binCounts[ind] + 1.0;
                    count = count + 1.0;
                }
            }
            else 
            {
                count = withdrawals.Count();
                binCounts[binCounts.Length - 1] = count;
            }

            this.WDistrib = new double[globals.WDBins];
            for (int i = 0; i < globals.WDBins; i++)
                WDistrib[i] = binCounts[i] / count;
        }
        public double TrailingAmount { get; set; }
        public double WithdrawalAver { get; set; }
        public double WithdrawalMin { get; set; }
        public double WithdrawalMax { get; set; }
        public double InsufficientWdRrate { get; set;  }
        public double[] WDistrib{ get; set; }
        public string Country { get; set; }
    }
}