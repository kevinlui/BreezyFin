using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;


using Breezy.Fin.Model;
using Breezy.Fin.Model.IB;
using IBApi;


namespace Breezy.Fin.Model.Factories
{
    public class IBApiFactory
    {
        // Use DllImport to import the Win32 SetForegroundWindow function.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);


        public static EWrapper CreateIBApi(Dispatcher mainThreadDispatcher)
        {
            //
            // Connect to the Java client
            //
            var ibClient = new EWrapperImpl(mainThreadDispatcher);
            ibClient.ClientSocket.eConnect("localhost", 7496, 0);

            //
            // Try to ensure there's a running IB Client
            //
            Thread.Sleep(2000);
            var processes = Process.GetProcesses().Where(p => p.MainWindowTitle.Contains("IB Trader Work"));
            if (processes.Count() != 1)
                return null;

            //
            // Automate clicking of IB Java dialog window, to confirm the expected prompt
            //
            var handle = processes.ElementAt(0).MainWindowHandle;
            SetForegroundWindow(handle);
            //await Task.Delay(500);
            SendKeys.SendWait("{ENTER}");
            //await Task.Delay(2000);
            Thread.Sleep(3000);

            return ibClient;

        }
    }
}
