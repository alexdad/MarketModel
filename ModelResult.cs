using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
    public class ModelResult
    {
        public ModelResult(Model m, List<SingleRunResult> results)
        {
            this.model = m;
            this.WDistrib = new double[Globals.Singleton().WDBins];
            for (int i = 0; i < Globals.Singleton().WDBins; i++)
            {
                this.WDistrib[i] = 0;
                foreach (var r in results)
                    this.WDistrib[i] = this.WDistrib[i] + r.WDistrib[i];
                this.WDistrib[i] /= results.Count;
            }

            int failures = 0, successes = 0;
            trailSuccessRate = Market.CheckTrailingAmount(results, ref failures, ref successes);

            withdrawalSuccessRate = Market.CheckWithdrawals(results, ref failures, ref successes);
            trailAverage = withdrawalAverage = 0;
            trailMin = withdrawalMin = double.MaxValue;
            trailMax = withdrawalMax = double.MinValue;

            overallSuccessRate = Market.CheckOverall(results, ref failures, ref successes);

            int count = 0;
            foreach (var sr in results)
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
            this.productivity = this.withdrawalAverage * 1000.0 / Globals.Singleton().StartSum * 100.0;
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
        public double productivity;
        public double[] WDistrib;
    }

}