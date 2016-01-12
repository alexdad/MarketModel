using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
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