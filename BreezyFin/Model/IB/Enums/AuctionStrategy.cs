using System;

namespace Breezy.Fin
{
    /// <summary>
    /// Order Auction Strategy
    /// </summary>
    [Serializable()] 
    public enum AuctionStrategy : int
    {
        /// <summary>
        /// Undefined
        /// </summary>
        Undefined = 0,
        /// <summary>
        /// Auction Match
        /// </summary>
        AuctionMatch = 1,
        /// <summary>
        /// Auction Improvement
        /// </summary>
        AuctionImprovement = 2,
        /// <summary>
        /// Auction Transparent
        /// </summary>
        AuctionTransparent = 3
    }
}