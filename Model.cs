using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{

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
        public int StartEq { get; set; }
        public int StartBo { get; set; }
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
}