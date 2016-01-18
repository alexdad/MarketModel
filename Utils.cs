using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FinancialModelB
{
    public class Utils
    {
        public static int PercentageScale { get { return 10000; } }
        public static double StepsInYear { get { return 1239.0 / 114.0; } }

        public const string ResultHeader = "Country,Strategy,Eq,Bo,Withdrawal,Rebalance,TrailAver,TrailMax,TrailMin,WDAver,WDMax,WDMin,Q1,Q2,Q3,Q4,Q5,Productivity,TrailSuccess,WDSuccess,SuccessRate, ";

        public const string ResultFormat = "{0},{1},{2},{3},{4:F2},{5},{6:F2},{7:F2},{8:F2},{9:F0},{10:F0},{11:F0},{12}{13:F2},{14:F2},{15:F2},{16:F2},";

        private static string s_ResultDirectory;
        private static Object s_ResultDirectoryProtection = new Object();
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
                                    mr.productivity,
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
                    mr.productivity,
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
            double productivity,
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
                                    productivity,
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
                                    productivity,
                                    trailSuccess,
                                    wdSuccess,
                                    successRate);
            };
        }

        public static string Init(string prefix)
        {
            lock (s_ResultDirectoryProtection)
            {
                if (s_ResultDirectory == null)
                    s_ResultDirectory = ResultDir(prefix);
            }
            return s_ResultDirectory;
        }
        public static string CommandFileName(string prefix)
        {
            Init(prefix);
            return String.Format("{0}\\Run.cmd", s_ResultDirectory);
        }

        public static string SummaryFileName(string prefix)
        {
            Init(prefix);
            return String.Format("{0}\\Summary.csv", s_ResultDirectory);
        }

        public static string ResultFileName(string prefix)
        {
            Init(prefix);
            return String.Format("{0}\\Results.csv", s_ResultDirectory);
        }
        public static string ResultDir(string prefix)
        {
            return String.Format("{0}\\{1}-{2:00}{3:00}{4:00}-{5:00}{6:00}{7:00}",
                Globals.Singleton().ResultLocation, prefix,
                DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
        }

        public static void CreateResultDir(
            string prefix, 
            string globalsFileName, 
            string countriesFileName, 
            string modelsFileName)
        {
            string dir = Init(prefix);
            if (Directory.Exists(dir))
                throw new Exception("Result doir exists");

            Directory.CreateDirectory(dir);
            File.Copy(globalsFileName, dir + "\\" + globalsFileName);
            File.Copy(countriesFileName, dir + "\\" + countriesFileName);
            File.Copy(modelsFileName, dir + "\\" + modelsFileName);
        }

        public static void SaveCommand(string filePath, string[] args)
        {
            using(StreamWriter sw = new StreamWriter(filePath))
            {
                sw.Write("{0}  ", Process.GetCurrentProcess().MainModule.FileName);
                for (int i=0; i < args.Length; i++)
                    sw.Write("{0}  ", args[i]);
                sw.WriteLine();
            }
        }
        public static SweepParameters[] Factorize(Factor[] factors, List<Country> countries)
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
                            sweeps = new SweepParameters[oldSweeps.Length * Globals.Singleton().SweepStrategies.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < Globals.Singleton().SweepStrategies.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].Strategy = Globals.Singleton().SweepStrategies[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.Withdrawal:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * Globals.Singleton().SweepWithdrawalRates.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < Globals.Singleton().SweepWithdrawalRates.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].WithdrawalRate = Globals.Singleton().SweepWithdrawalRates[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.WorldShare:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * Globals.Singleton().SweepWorldShares.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < Globals.Singleton().SweepWorldShares.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].WorldShare = Globals.Singleton().SweepWorldShares[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.Equity:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * Globals.Singleton().SweepEquities.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < Globals.Singleton().SweepEquities.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].Equity = Globals.Singleton().SweepEquities[n];
                                    c++;
                                }
                            }
                        }
                        break;
                    case Factor.Bonds:
                        {
                            SweepParameters[] oldSweeps = sweeps;
                            sweeps = new SweepParameters[oldSweeps.Length * Globals.Singleton().SweepBonds.Length];
                            int c = 0;
                            for (int o = 0; o < oldSweeps.Length; o++)
                            {
                                for (int n = 0; n < Globals.Singleton().SweepBonds.Length; n++)
                                {
                                    sweeps[c] = oldSweeps[o];
                                    sweeps[c].Bonds = Globals.Singleton().SweepBonds[n];
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
}