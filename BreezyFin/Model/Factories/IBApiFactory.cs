using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;


using Breezy.Fin.Model;
using Breezy.Fin.Model.IB;
using IBApi;


namespace Breezy.Fin.Model.Factories
{
    public class IBApiFactory
    {
        public static EWrapper CreateIBApi(Dispatcher mainThreadDispatcher)
        {
            var ibClient = new EWrapperImpl(mainThreadDispatcher);
            ibClient.ClientSocket.eConnect("localhost", 7496, 0);

            Thread.Sleep(10000);

            return ibClient;

        }
    }
}
