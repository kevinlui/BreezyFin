using System;

namespace Breezy.Fin
{
    /// <summary>
    /// Market Depth Side
    /// </summary>
    [Serializable()] 
    public enum MarketDepthSide : int
    {
        /// <summary>
        /// Ask Price Side
        /// </summary>
        Ask = 0,
        /// <summary>
        /// Bid Price Side
        /// </summary>
        Bid = 1
    }
}