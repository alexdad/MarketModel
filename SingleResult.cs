using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
    public class SingleRunResult
    {
        public SingleRunResult(string country, Model m,
            double trailingAmount, double[] withdrawals)
        {
            this.Country = country;
            this.TrailingAmount = trailingAmount;
            SetWD(m, withdrawals);
        }

        public SingleRunResult(string country, Model m,
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

            SetWD(m, withdrawals);
        }

        private void SetWD(Model m, double[] withdrawals)
        {
            double essentialWD = Globals.Singleton().EssentialsPercent / 100.0 * Globals.Singleton().StartSum * Market.NormativeStepWD(m);
            int nSmallishWD = 0;
            foreach (var w in withdrawals)
            {
                if (w < essentialWD)
                    nSmallishWD++;
            }
            this.InsufficientWdRrate = (double)nSmallishWD * 100.0 / (double)withdrawals.Length;

            double[] binCounts = new double[Globals.Singleton().WDBins];
            for (int i = 0; i < binCounts.Length; i++)
                binCounts[i] = 0.0;

            this.WithdrawalAver = withdrawals.Average();
            this.WithdrawalMax = withdrawals.Max();
            this.WithdrawalMin = withdrawals.Min();
            double count = 0;
            double norm = Globals.Singleton().StartSum * Market.NormativeStepWD(m);
            foreach (double wd in withdrawals)
            {
                int ind = Globals.Singleton().Quantile(wd / norm);
                binCounts[ind] = binCounts[ind] + 1.0;
                count = count + 1.0;
            }

            this.WDistrib = new double[Globals.Singleton().WDBins];
            for (int i = 0; i < Globals.Singleton().WDBins; i++)
                WDistrib[i] = binCounts[i] / count;
        }
        public double TrailingAmount { get; set; }
        public double WithdrawalAver { get; set; }
        public double WithdrawalMin { get; set; }
        public double WithdrawalMax { get; set; }
        public double InsufficientWdRrate { get; set; }
        public double[] WDistrib { get; set; }
        public string Country { get; set; }
    }

}