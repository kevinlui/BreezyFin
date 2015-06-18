using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



namespace Breezy.Fin.Model.Factories
{
    public class BrokerFactory
    {
        public static IBroker CreateBroker()
        {
            return new MockBroker();
        }
    }
}
