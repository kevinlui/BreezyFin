using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Input;


using Breezy.Fin.Model.Factories;
using Breezy.Fin.Model.IB;
using Breezy.Fin.Command;
using Breezy.Fin.Model;


namespace Breezy.Fin.ViewModel
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly EWrapperImpl eWrapperImpl;

        private readonly ButtonICommand cmdCalcPortfolioTotal;

        public MainWindowViewModel()
        {
            /*
            var lstAssets = BrokerFactory.CreateBroker().GetAssets();
            this.DataContext = lstAssets;
             */

            cmdCalcPortfolioTotal = new ButtonICommand(Portfolio.Instance.CalcPortfolioTotal, Portfolio.Instance.CanCalcPortfolioTotal);

            IBApi.EWrapper eWrapper = IBApiFactory.CreateIBApi(Dispatcher.CurrentDispatcher);
            eWrapperImpl = (eWrapper as EWrapperImpl);

            eWrapperImpl.ClientSocket.reqPositions();
        }

        public ObservableCollection<Asset> PortfolioAssets
        {
            get
            {
                return Portfolio.Instance.Assets;
            }
        }

        private double? _protfolioTotal;
        public double? PortfolioTotal
        {
            get { return this._protfolioTotal;  }
        }

        public void CalculatePortfolio()
        {
            // call into Model's class (Portfolio) CalcPortfolioTotal
            Portfolio.Instance.CalcPortfolioTotal();
            _protfolioTotal = Portfolio.Instance.PortfolioTotal;

            RaisePropertyChangeEvent("PortfolioTotal");
        }

        public ICommand BtnClickCalcPortfolioTotal
        {
            get
            {
                return cmdCalcPortfolioTotal;
            }
        }

        public void DisconnectIB()
        {
            if (eWrapperImpl != null)
                eWrapperImpl.ClientSocket.eDisconnect();
        }

        private void RaisePropertyChangeEvent(String propertyName)
        {
            if (PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
