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
        public double WorldShare;
        public int Equity;
        public int Bonds;
        public int Strategy;
    }

    public struct SweepStat
    {
        public Dictionary<int, List<ModelResult>> sweepResults;
        public double weightedProd;
        public double weightedSuccessRate;
        public double totalPop;
    }

    public struct SweepResult
    {
        public SweepParameters parameters;
        public SweepStat stat;
    }

}