using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace FinancialModelB
{
    public class Country
    {
        public Country(string fname, int bp, int tp, double le, double lbo, double lbi, double population, int weight)
        {
            Filename = fname;
            BottomPower = bp;
            TopPower = tp;
            LastEquity = le;
            LastBond = lbo;
            LastBill = lbi;
            Population = population;
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
        public double Population { get; set; }
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
                int weight = int.Parse(values[7]);
                if (weight > 0)
                {
                    list.Add(new Country(
                        values[0],
                        int.Parse(values[1]),
                        int.Parse(values[2]),
                        double.Parse(values[3]),
                        double.Parse(values[4]),
                        double.Parse(values[5]),
                        double.Parse(values[6]),
                        weight));
                }
            }

            return list;
        }
    }

}