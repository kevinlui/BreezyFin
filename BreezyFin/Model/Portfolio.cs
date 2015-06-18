using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breezy.Fin.Model
{
    public class Portfolio : INotifyCollectionChanged
    {
        public static readonly Portfolio Instance = new Portfolio();

        /// <summary>Event raised when the collection changes.</summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ObservableCollection<Asset> Assets = new ObservableCollection<Asset>();

        public void Populate(IEnumerable<Asset> assets)
        {
            this.Assets.Clear();
            foreach (Asset a in assets)
                this.Assets.Add(a);

            //if (Instance.CollectionChanged != null)
            //    Instance.CollectionChanged(Instance, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private double _portfolioTotal;
        public double PortfolioTotal
        {
            get
            {
                return this._portfolioTotal;
            }
        }

        public void CalcPortfolioTotal()
        {
            this._portfolioTotal = Instance.Assets.Select<Asset, double>(s => s.MarketValue.Value).Sum();
            foreach (Asset a in Instance.Assets)
                a.Percentage = a.MarketValue.Value / _portfolioTotal;

            //double total = Positions.Select<Asset, double>(s => s.MarketValue).Sum();

            //Parallel.ForEach<Asset>(Positions, a => { a.Percentage = a.MarketValue / total; });
            //foreach (Asset a in Positions)
            //    a.Percentage = a.MarketValue / total;
        }

        public bool CanCalcPortfolioTotal()
        {
            return ( Instance.Assets != null && Instance.Assets.Any() );
        }
    }
}
