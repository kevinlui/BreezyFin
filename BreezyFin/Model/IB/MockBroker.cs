using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

using Breezy.Fin.ViewModel;


namespace Breezy.Fin.Model
{
    public class MockBroker : IBroker
    {
        void IBroker.Connect()
        {
        }

        ObservableCollection<Asset> IBroker.GetAssets()
        {
            var assets = new ObservableCollection<Asset>{
                new Asset
                {
                    Symbol = "CMCM",
                    Name = "Cheetah Mobile",
                    Market = "NYSE",
                    Position = 3000,
                    AvgCost = 28.8,
                    MarketPrice = 33.46,
                    FxRate = 7.8 
                },
                new Asset 
                {
                    Symbol = "BIDU",
                    Name = "Baidu",
                    Market = "NASDAQ",
                    Position = 100,
                    AvgCost = 192.8,
                    MarketPrice = 203.15,
                    FxRate = 7.8
                },
                new Asset
                {
                    Symbol = "TSLA",
                    Name = "Tesla",
                    Market = "NASDAQ",
                    Position = 100,
                    AvgCost = 235,
                    MarketPrice = 247.8,
                    FxRate = 7.8
                },
                new Asset
                {
                    Symbol = "700",
                    Name = "Tencent",
                    Market = "HKSE",
                    Position = 9000,
                    AvgCost = 131,
                    MarketPrice = 158.8
                },
                new Asset
                {
                    Symbol = "354",
                    Name = "China Software",
                    Market = "HKSE",
                    Position = 100000,
                    AvgCost = 5.23,
                    MarketPrice = 5.05
                },
                new Asset
                {
                    Symbol = "686",
                    Name = "Solar",
                    Market = "HKSE",
                    Position = 200000,
                    AvgCost = 1.45,
                    MarketPrice = 1.46
                },
                new Asset
                {
                    Symbol = "1211",
                    Name = "BYD",
                    Market = "HKSE",
                    Position = 4000,
                    AvgCost = 54,
                    MarketPrice = 54.5
                }
            };


            return assets;
        }
    }
}
