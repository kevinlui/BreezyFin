using System;
using System.Collections.Generic;
using System.Diagnostics;

using Breezy.Fin.Model;
using Breezy.Fin.Model.Factories;
using Breezy.Fin.ViewModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Breezy.Fin.UnitTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestBrokerFactory()
        {
            //
            // Covariance demo
            //
            var assets = BrokerFactory.CreateBroker().GetAssets();

            // Covariance:
            // - Caller on IEnumerable<out T> should be ALLOWED to operate on the result as a more base type, 
            //   thus IEnumerable<out T> cast to IEnumerable<Base> should work
            IEnumerable<AssetBase> iBase = assets as IEnumerable<AssetBase>;    // successful cast
            Assert.AreNotEqual(iBase, null);

            // Covariance:
            // - Caller on IEnumerable<out T> should be DISALLOWED to operate on the result as a more derived type, 
            //   thus IEnumerable<out T> cast to IEnumerable<Derived> should not work
            IEnumerable<AssetDerived> iDerived = assets as IEnumerable<AssetDerived>;   // null
            Assert.AreEqual(iDerived, null);

            //
            // Contravariance demo
            //
            Action<Asset> aAsset = new Action<Asset>(a =>
            {
                a.MarketValue = a.MarketPrice * Math.Abs(a.Position) * a.FxRate;
            });

            // Contravariance: 
            // - Caller on Action<in T> should be DISALLOWED to pass in a more base type, 
            //   thus Action<T> cast to Action<Base> should not work
            Action<AssetBase> aAssetBase = aAsset as Action<AssetBase>;     // null
            Assert.AreEqual(aAssetBase, null);

            // Contravariance: 
            // - Caller on Action<in T> should be ALLOWED to pass in a more derived type, 
            //   thus Action<T> cast to Action<Derived> should work
            Action<AssetDerived> aAssetDerived = aAsset;    // successful cast
            Assert.AreNotEqual(aAssetDerived, null);

            aAssetDerived(new AssetDerived());
        }
    }
}
