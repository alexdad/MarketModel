using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
    public class ColorSorter : IComparer<KeyValuePair<int, Color>>
    {
        public int Compare(KeyValuePair<int, Color> c1, KeyValuePair<int, Color> c2)
        {
            return c2.Key.CompareTo(c1.Key);
        }
    }

    public class GraphAcquierer
    {
        enum ColorRoles { Red = 0, Gray, Blue, Black, White };
        const int nColors = 5;
        const int nGraphs = 3;
        const int minStretch = 2;

        static ColorRoles FindClosest(Color c, Color[] typicals)
        {
            double minDiff = 3 * (255 * 255);
            ColorRoles minIndex = ColorRoles.White;

            for (ColorRoles cr = ColorRoles.Red; cr <= ColorRoles.White; cr++)
            {
                Color tc = typicals[(int)cr];
                double diff = (tc.R - c.R) * (tc.R - c.R) + (tc.G - c.G) * (tc.G - c.G) + (tc.B - c.B) * (tc.B - c.B);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    minIndex = cr;
                }
            }
            return minIndex;
        }

        static void Smooth(int[] vals)
        {
            const int maxGapHor = 70;
            const int maxGapVert = 70;

            int last = -1;
            for (int c = 0; c < vals.Length; c++)
            {
                if (vals[c] >= 0 &&
                    c > last + 1 &&
                    last >= 0 &&
                    c - last < maxGapHor &&
                    Math.Abs(vals[c] - vals[last]) < maxGapVert)
                {
                    int delta = (vals[c] - vals[last]) / (c - last);
                    for (int i = last + 1; i < c; i++)
                    {
                        vals[i] = vals[i - 1] + delta;
                    }
                }
                if (vals[c] >= 0)
                    last = c;
            }

            for (int c = 1; c < vals.Length - 1; c++)
            {
                if (vals[c] > 0 &&
                    vals[c - 1] > 0 &&
                    vals[c + 1] > 0)
                {
                    int av = (vals[c - 1] + vals[c + 1]) / 2;
                    int delta = Math.Abs((vals[c] - av));
                    if (delta > av / 2)
                    {
                        if (vals[c] > vals[c - 1] && vals[c] > vals[c + 1] ||
                            vals[c] < vals[c - 1] && vals[c] < vals[c + 1])
                            vals[c] = av;
                    }
                }
            }
        }

        static double Transform(int h, Country cn, int lowLine, int highLine)
        {
            if (h >= 0)
                return Math.Pow(10.0, (double)(h - lowLine) / (double)(highLine - lowLine) * (cn.TopPower - cn.BottomPower) + cn.BottomPower);
            else
                return 0.0;
        }

        static void CountChanges(double oldVal, double newVal, int weight, List<int> changes)
        {
            int prc = (int)((newVal - oldVal) / oldVal * Params.PercentageScale);
            const double minOldValueToCount = 0.001;
            if (Math.Abs(oldVal) < minOldValueToCount)
                return;

            if (prc > Params.PercentageScale)
                Console.WriteLine("Skipping suspicious change: percentage = {0}", (double)prc / (double)Params.PercentageScale * 100.0);
            else
            {
                for (int w = 0; w < weight; w++)
                    changes.Add(prc);
            }
        }

        static public void Acquire(
            List<Country> countries,
            List<int> equityChanges, 
            List<int> bondChanges, 
            List<int> billChanges)
        {
            Color[] typicals = new Color[nColors];
            int[] counters = new int[nColors];
            string[] names = new string[] { "Red", "Gray", "Blue", "Black", "White" };
            typicals[(int)ColorRoles.Red] = Color.FromArgb(162, 25, 43);
            typicals[(int)ColorRoles.Gray] = Color.FromArgb(142, 135, 130);
            typicals[(int)ColorRoles.Blue] = Color.FromArgb(12, 52, 104);
            typicals[(int)ColorRoles.Black] = Color.FromArgb(0, 0, 0);
            typicals[(int)ColorRoles.White] = Color.FromArgb(255, 255, 255);

            double maxEquity = double.MinValue, maxBond = double.MinValue, maxBill = double.MinValue;
            double minEquity = double.MaxValue, minBond = double.MaxValue, minBill = double.MaxValue;
            int cntEquity = 0, cntBond = 0, cntBill = 0;

            foreach (Country cn in countries)
            {
                string jpgPath = cn.Filename;
                string country = cn.Filename.Replace(".JPG", "").Replace(".jpg", "");
                string csvPath = country + ".csv";

                if (cn.Weight == 0)
                {
                    Console.WriteLine("Skipping {0}", country);
                    continue;
                }

                Dictionary<ColorRoles, int> colorCounts = new Dictionary<ColorRoles, int>();
                Bitmap img = new Bitmap(jpgPath);
                for (int r = 0; r < img.Height; r++)
                {
                    for (int c = 0; c < img.Width; c++)
                    {
                        Color clr = img.GetPixel(c, r);
                        ColorRoles cr = FindClosest(clr, typicals);
                        counters[(int)cr]++;
                    }
                }

                int[] reds = new int[img.Width];
                int[] grays = new int[img.Width];
                int[] blues = new int[img.Width];
                int lowestBlack = -1, highestBlack = -1;

                double[] equities = new double[img.Width];
                double[] bonds = new double[img.Width];
                double[] bills = new double[img.Width];

                using (StreamWriter sw = new StreamWriter(csvPath))
                {
                    for (int r = 0; r < img.Height; r++)
                    {
                        int nBlack = 0;
                        for (int c = 0; c < img.Width; c++)
                        {
                            Color clr = img.GetPixel(c, r);
                            ColorRoles cr = FindClosest(clr, typicals);
                            if (cr == ColorRoles.Black)
                                nBlack++;
                        }
                        if (nBlack > 3 * img.Width / 4)
                        {
                            lowestBlack = r;
                            break;
                        }
                    }

                    for (int r = img.Height - 1; r > lowestBlack; r--)
                    {
                        int nBlack = 0;
                        for (int c = 0; c < img.Width; c++)
                        {
                            Color clr = img.GetPixel(c, r);
                            ColorRoles cr = FindClosest(clr, typicals);
                            if (cr == ColorRoles.Black)
                                nBlack++;
                        }
                        if (nBlack > 3 * img.Width / 4)
                        {
                            highestBlack = r;
                            break;
                        }
                    }

                    int lowLine = img.Height - highestBlack;
                    int highLine = img.Height - lowestBlack;

                    // Console.WriteLine("{0}: low={1}, high = {2}", jpgPath, lowLine, highLine);

                    for (int c = 0; c < img.Width; c++)
                    {
                        StringBuilder sb = new StringBuilder();
                        int curCrr = (int)ColorRoles.Black;
                        int firstCur = -1;
                        int[] maxStretch = new int[nGraphs];
                        int[] bestStretch = new int[nGraphs];
                        for (int i = 0; i < nGraphs; i++)
                        {
                            maxStretch[i] = -1;
                            bestStretch[i] = -1;
                        }

                        for (int r = 0; r < img.Height; r++)
                        {
                            Color clr = img.GetPixel(c, r);
                            ColorRoles cr = FindClosest(clr, typicals);
                            int crr = (int)cr;
                            if (c == 640)
                                sb.Append(crr.ToString());

                            if (crr != curCrr)
                            {
                                if (firstCur >= 0)
                                {
                                    int stretch = r - firstCur;
                                    if (stretch > minStretch && curCrr < nGraphs && curCrr >= 0)
                                    {
                                        if (maxStretch[curCrr] < stretch)
                                        {
                                            maxStretch[curCrr] = stretch;
                                            bestStretch[curCrr] = (r + firstCur) / 2;
                                        }
                                    }
                                }
                                firstCur = r;
                                curCrr = crr;
                            }
                        }

                        //if (c == 640)
                        //    Console.WriteLine(sb.ToString());

                        {
                            reds[c] = grays[c] = blues[c] = -1;

                            int crr = (int)ColorRoles.Red;
                            if (maxStretch[crr] > minStretch)
                                reds[c] = img.Height - bestStretch[crr];

                            crr = (int)ColorRoles.Gray;
                            if (maxStretch[crr] > minStretch)
                                grays[c] = img.Height - bestStretch[crr];

                            crr = (int)ColorRoles.Blue;
                            if (maxStretch[crr] > minStretch)
                                blues[c] = img.Height - bestStretch[crr];
                        }
                    }

                    // Fill up the gaps
                    Smooth(reds);
                    Smooth(grays);
                    Smooth(blues);

                    double lastEquity = -1, lastBond = -1, lastBill = -1;

                    for (int c = 0; c < img.Width - 2; c++)
                    {
                        equities[c] = Transform(blues[c], cn, lowLine, highLine);
                        bonds[c] = Transform(grays[c], cn, lowLine, highLine);
                        bills[c] = Transform(reds[c], cn, lowLine, highLine);

                        if (equities[c] != 0)
                        {
                            cntEquity++;
                            lastEquity = equities[c];
                            minEquity = Math.Min(minEquity, equities[c]);
                            maxEquity = Math.Max(maxEquity, equities[c]);
                            if (c > 0 && equities[c - 1] > 0)
                                CountChanges(equities[c - 1], equities[c], cn.Weight, equityChanges);
                        }
                        if (bonds[c] != 0)
                        {
                            cntBond++;
                            lastBond = bonds[c];
                            minBond = Math.Min(minBond, bonds[c]);
                            maxBond = Math.Max(maxBond, bonds[c]);
                            if (c > 0 && bonds[c - 1] > 0)
                                CountChanges(bonds[c - 1], bonds[c], cn.Weight, bondChanges);
                        }
                        if (bills[c] != 0)
                        {
                            cntBill++;
                            lastBill = bills[c];
                            minBill = Math.Min(minBill, bills[c]);
                            maxBill = Math.Max(maxBill, bills[c]);
                            if (c > 0 && bonds[c - 1] > 0)
                                CountChanges(bills[c - 1], bills[c], cn.Weight, billChanges);
                        }
                    }

                    // Write out
                    for (int c = 0; c < img.Width - 2; c++)
                    {
                        sw.WriteLine("{0},{1},{2},{3},{4},{5},",
                            blues[c], grays[c], reds[c],
                            equities[c], bonds[c], bills[c]);
                    }

                    double equityError = (cn.LastEquity - lastEquity) / cn.LastEquity;
                    double bondError = (cn.LastBond - lastBond) / cn.LastBond;
                    double billError = (cn.LastBill - lastBill) / cn.LastBill;
                    Console.WriteLine("{0}: {1:F2}, {2:F2}, {3:F2}, equity: {4:F2} vs {5:F2}, bonds {6:F2} vs {7:F2}, bills {8:F2} vs {9:F2}",
                        country,
                        equityError, bondError, billError,
                        lastEquity, cn.LastEquity, lastBond, cn.LastBond, lastBill, cn.LastBill);
                }

            }

            Console.WriteLine();
            Console.WriteLine("Equities: {0} from {1} to {2}", cntEquity, minEquity, maxEquity);
            Console.WriteLine("Bonds: {0} from {1} to {2}", cntBond, minBond, maxBond);
            Console.WriteLine("Bills: {0} from {1} to {2}", cntBill, minBill, maxBill);

            Console.WriteLine();
            Console.WriteLine("Equity changes: {0} from {1} to {2}", equityChanges.Count, equityChanges.Min(), equityChanges.Max());
            Console.WriteLine("Bonds changes: {0} from {1} to {2}", bondChanges.Count, bondChanges.Min(), bondChanges.Max());
            Console.WriteLine("Bills changes: {0} from {1} to {2}", billChanges.Count, billChanges.Min(), billChanges.Max());
        }
    }
}