using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breezy.Fin.Model.IB.MarketData
{
    public class CcyExchangeRate
    {
        public static Dictionary<string, double> Table = new Dictionary<string, double>
        {
            { "HKD", 1 }, 
            { "USD", 7.785 }
        };
    }
}
