using System;

namespace Breezy.Fin
{
    /// <summary>
    /// Connection Closed Event Arguments
    /// </summary>
    [Serializable()]
    public class ConnectionClosedEventArgs : EventArgs
    {
        /// <summary>
        /// Uninitialized Constructor for Serialization
        /// </summary>
        public ConnectionClosedEventArgs()
        {
            
        }
    }
}