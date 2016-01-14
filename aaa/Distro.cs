using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
    public class DistroBin
    {
        public DistroBin(double mid, double prob)
        {
            this.Midpoint = mid;
            this.AccumulatedProbability = prob;
        }
        public double Midpoint { get; set; }
        public double AccumulatedProbability { get; set; }
    }

    public class DistroBinComparer : IComparer<DistroBin>
    {
        public int Compare(DistroBin x, DistroBin y)
        {
            return x.AccumulatedProbability.CompareTo(y.AccumulatedProbability);
        }
    }
    public class Distro
    {
        public Distro(int bins)
        {
            this.bins = bins;
            this.curAccumulatedProb = 0;
            this.rnd = new Random();
            this.accumulatedProbability = new List<DistroBin>();
            this.rndLock = new Object();
        }
        public void AddBin(double mid, double prob)
        {
            curAccumulatedProb += prob;
            accumulatedProbability.Add(new DistroBin(mid, curAccumulatedProb));
        }

        public double GetAccumulation()
        {
            return curAccumulatedProb;
        }

        public double Play()
        {
            double next = 0;
            lock (rndLock)
            {
                next = rnd.NextDouble();
            }
            int ind = accumulatedProbability.BinarySearch(new DistroBin(0, next), comparer);
            if (ind < 0)
                ind = ~ind;

            return accumulatedProbability[ind].Midpoint / Params.PercentageScale;
        }

        public static void PrepareDistribution(List<int> changes, Distro distro, int bins, string resultPath)
        {
            int minChange = changes.Min();
            int maxChange = changes.Max();
            int span = maxChange - minChange;
            if (span == 0)
            {
                Console.WriteLine("CANNOT CREATE {0} - no changes: {1} , { 2}", resultPath, minChange, maxChange);
                return;
            }

            int[] counts = new int[bins];
            for (int i = 0; i < bins; i++)
                counts[i] = 0;
            int binSize = span / bins + 1;
            foreach (int c in changes)
            {
                int binNo = (c - minChange) / binSize;
                counts[binNo]++;
            }

            using (StreamWriter sw = new StreamWriter(resultPath))
            {
                for (int j = 0; j < bins; j++)
                {
                    double mid = minChange + binSize / 2 + binSize * j;
                    double binProb = (double)counts[j] / (double)changes.Count;
                    sw.WriteLine("{0:F2},{1:F2},", mid, binProb * 100.0);

                    distro.AddBin(mid, binProb);
                }
            }

            Console.WriteLine("Accumulated: {0}", distro.GetAccumulation());
        }

        private int bins;
        private double curAccumulatedProb;
        private Random rnd;
        Object rndLock;

        private List<DistroBin> accumulatedProbability;
        private DistroBinComparer comparer = new DistroBinComparer();
    }

}