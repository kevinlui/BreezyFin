using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Breezy.Fin.Model
{
    public class AssetBase
    {

    }

    public class Asset : AssetBase, INotifyPropertyChanged
    {
        private String _symbol;
        public String Symbol
        {
            get { return _symbol; }
            set {
                _symbol = value;
                raisePropertyChangeEvent("Symbol");
            }
        }

        private String _name;
        public String Name
        {
            get { return _name; }
            set
            {
                _name = value;
                raisePropertyChangeEvent("Name");
            }
        }

        private double _position;
        public double Position
        {
            get { return _position; }
            set
            {
                _position = value;
                raisePropertyChangeEvent("Position");
            }
        }

        private string _currency;
        public string Currency
        {
            get { return _currency; }
            set
            {
                _currency = value;
                raisePropertyChangeEvent("Currency");
            }
        }

        private double _fxRate = 1;
        public double FxRate
        {
            get { return _fxRate; }
            set
            {
                _fxRate = value;
                raisePropertyChangeEvent("FxRate");
            }
        }

        private double _avgCost;
        public double AvgCost
        {
            get { return _avgCost; }
            set {
                _avgCost = value;
                raisePropertyChangeEvent("AvgCost");
            }
        }

        private double? _marketPrice;
        public double? MarketPrice
        {
            get { return _marketPrice; }
            set {
                _marketPrice = value;
                raisePropertyChangeEvent("MarketPrice");
            }
        }

        private double? _marketValue = null;
        public double? MarketValue
        {
            set { _marketValue = value; }
            get { return (_marketPrice.HasValue ? _marketPrice * Math.Abs(_position) * _fxRate : null); }
        }

        private string _market;
        public string Market
        {
            get { return _market; }
            set {
                _market = value;
                raisePropertyChangeEvent("Market");
            }
        }

        private double _percentage;
        public double Percentage
        {
            get { return _percentage; }
            set 
            { 
                _percentage = value;
                raisePropertyChangeEvent("Percentage");
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void raisePropertyChangeEvent(String propertyName)
        {
            if (PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class AssetDerived : Asset
    { }
}
