using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace Breezy.IB
{
    /// <summary>
    /// Interactive Brokers Client
    /// Handles all communications to and from the TWS.
    /// </summary>
    public class IBClient_Mock : IBClient
    {
        #region Tracer
        private GeneralTracer ibTrace = new GeneralTracer("ibInfo", "Interactive Brokers Parameter Info");
        private GeneralTracer ibTickTrace = new GeneralTracer("ibTicks", "Interactive Brokers Tick Info");
        #endregion

        #region Constructor / Destructor

        /// <summary>
        /// Default Constructor
        /// </summary>
        public IBClient_Mock() : base()
        {
        }

        #endregion

        #region Network Commmands

        /// <summary>
        /// This function must be called before any other. There is no feedback for a successful connection, but a subsequent attempt to connect will return the message "Already connected."
        /// </summary>
        /// <param name="host">host name or IP address of the machine where TWS is running. Leave blank to connect to the local host.</param>
        /// <param name="port">must match the port specified in TWS on the Configure>API>Socket Port field.</param>
        /// <param name="clientId">A number used to identify this client connection. All orders placed/modified from this client will be associated with this client identifier.
        /// Each client MUST connect with a unique clientId.</param>
        public override void Connect(String host, int port, int clientId)
        {
   
            if (host == null)
                throw new ArgumentNullException("host");
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException("port");
            lock (this)
            {
                // already connected?
                host = checkConnected(host);
                if (host == null)
                {
                    return;
                }
                TcpClient socket = new TcpClient(host, port);
                connect(socket, clientId);
            }
        }

        /// <summary>
        /// Call this method to terminate the connections with TWS. Calling this method does not cancel orders that have already been sent.
        /// </summary>
        public override void Disconnect()
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    return;
                }

                try
                {
                    // stop Reader thread
                    Stop();
                    readThread.Abort();

                    // close ibSocket
                    if (ibSocket != null)
                    {
                        ibSocket.Close();
                    }
                }
                catch
                {
                }
                connected = false;
            }
        }

        /// <summary>
        /// Call the cancelScannerSubscription() method to stop receiving market scanner results. 
        /// </summary>
        /// <param name="tickerId">the Id that was specified in the call to reqScannerSubscription().</param>
        public void CancelScannerSubscription(int tickerId)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                if (serverVersion < 24)
                {
                    error(ErrorMessage.UpdateTws, "It does not support API scanner subscription.");
                    return;
                }

                int version = 1;

                // send cancel mkt data msg
                try
                {
                    send((int) OutgoingMessage.CancelScannerSubscription);
                    send(version);
                    send(tickerId);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(tickerId, ErrorMessage.FailSendCancelScanner, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call the reqScannerParameters() method to receive an XML document that describes the valid parameters that a scanner subscription can have.
        /// </summary>
        public void RequestScannerParameters()
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                if (serverVersion < 24)
                {
                    error(ErrorMessage.UpdateTws, "It does not support API scanner subscription.");
                    return;
                }

                int version = 1;

                try
                {
                    send((int) OutgoingMessage.RequestScannerParameters);
                    send(version);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) | throwExceptions)
                        throw;
                    error(ErrorMessage.FailSendRequestScannerParameters, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call the reqScannerSubscription() method to start receiving market scanner results through the scannerData() EWrapper method. 
        /// </summary>
        /// <param name="tickerId">the Id for the subscription. Must be a unique value. When the subscription  data is received, it will be identified by this Id. This is also used when canceling the scanner.</param>
        /// <param name="subscription">summary of the scanner subscription parameters including filters.</param>
        public void RequestScannerSubscription(int tickerId, ScannerSubscription subscription)
        {
            if (subscription == null)
                throw new ArgumentNullException("subscription");
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                if (serverVersion < 24)
                {
                    error(ErrorMessage.UpdateTws, "It does not support API scanner subscription.");
                    return;
                }

                int version = 3;

                try
                {
                    send((int) OutgoingMessage.RequestScannerSubscription);
                    send(version);
                    send(tickerId);
                    sendMax(subscription.NumberOfRows);
                    send(subscription.Instrument);
                    send(subscription.LocationCode);
                    send(subscription.ScanCode);
                    sendMax(subscription.AbovePrice);
                    sendMax(subscription.BelowPrice);
                    sendMax(subscription.AboveVolume);
                    sendMax(subscription.MarketCapAbove);
                    sendMax(subscription.MarketCapBelow);
                    send(subscription.MoodyRatingAbove);
                    send(subscription.MoodyRatingBelow);
                    send(subscription.SPRatingAbove);
                    send(subscription.SPRatingBelow);
                    send(subscription.MaturityDateAbove);
                    send(subscription.MaturityDateBelow);
                    sendMax(subscription.CouponRateAbove);
                    sendMax(subscription.CouponRateBelow);
                    send(subscription.ExcludeConvertible);
                    if (serverVersion >= 25)
                    {
                        send(subscription.AverageOptionVolumeAbove);
                        send(subscription.ScannerSettingPairs);
                    }
                    if (serverVersion >= 27)
                    {
                        send(subscription.StockTypeFilter);
                    }
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;
                    error(tickerId, ErrorMessage.FailSendRequestScanner, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to request market data. The market data will be returned by the tickPrice, tickSize, tickOptionComputation(), tickGeneric(), tickString() and tickEFP() methods.
        /// </summary>
        /// <param name="tickerId">the ticker id. Must be a unique value. When the market data returns, it will be identified by this tag. This is also used when canceling the market data.</param>
        /// <param name="contract">this structure contains a description of the contract for which market data is being requested.</param>
        /// <param name="genericTickList">comma delimited list of generic tick types.  Tick types can be found here: (new Generic Tick Types page) </param>
        /// <param name="snapshot">Allows client to request snapshot market data.</param>
        /// <param name="marketDataOff">Market Data Off - used in conjunction with RTVolume Generic tick type causes only volume data to be sent.</param>
        public void RequestMarketData(int tickerId, Contract contract, Collection<GenericTickType> genericTickList, bool snapshot, bool marketDataOff)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                //35 is the minimum version for snapshots
                if (serverVersion < MinServerVersion.ScaleOrders && snapshot)
                {
                    error(tickerId, ErrorMessage.UpdateTws, "It does not support snapshot market data requests.");
                    return;
                }

                //40 is the minimum version for the Underlying Component class
                if (serverVersion < MinServerVersion.UnderComp)
                {
                    if (contract.UnderlyingComponent != null)
                    {
                        error(tickerId, ErrorMessage.UpdateTws, "It does not support delta-neutral orders.");
                        return;
                    }
                }

                //46 is the minimum version for requesting contracts by conid
                if (serverVersion < MinServerVersion.RequestMarketDataConId)
                {
                    if (contract.ContractId > 0)
                    {
                        error(tickerId, ErrorMessage.UpdateTws, "It does not support conId parameter.");
                        return;
                    }
                }

                int version = 9;

                try
                {
                    // send req mkt data msg
                    send((int) OutgoingMessage.RequestMarketData);
                    send(version);
                    send(tickerId);
                    if (serverVersion >= 47)
                        send(contract.ContractId);

                    //Send Contract Fields
                    send(contract.Symbol);
                    send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                    send(contract.Expiry);
                    send(contract.Strike);
                    send(((contract.Right == RightType.Undefined)
                              ? ""
                              : EnumDescConverter.GetEnumDescription(contract.Right)));
                    if (serverVersion >= 15)
                    {
                        send(contract.Multiplier);
                    }
                    send(contract.Exchange);
                    if (serverVersion >= 14)
                    {
                        send(contract.PrimaryExchange);
                    }
                    send(contract.Currency);
                    if (serverVersion >= 2)
                    {
                        send(contract.LocalSymbol);
                    }
                    if (serverVersion >= 8 && contract.SecurityType == SecurityType.Bag)
                    {
                        if (contract.ComboLegs == null)
                        {
                            send(0);
                        }
                        else
                        {
                            send(contract.ComboLegs.Count);

                            ComboLeg comboLeg;
                            for (int i = 0; i < contract.ComboLegs.Count; i++)
                            {
                                comboLeg = (ComboLeg) contract.ComboLegs[i];
                                send(comboLeg.ConId);
                                send(comboLeg.Ratio);
                                send(EnumDescConverter.GetEnumDescription(comboLeg.Action));
                                send(comboLeg.Exchange);
                            }
                        }
                    }

                    if (serverVersion >= 40)
                    {
                        if (contract.UnderlyingComponent != null)
                        {
                            UnderlyingComponent underComp = contract.UnderlyingComponent;
                            send(true);
                            send(underComp.ContractId);
                            send(underComp.Delta);
                            send(underComp.Price);
                        }
                        else
                        {
                            send(false);
                        }
                    }

                    if (serverVersion >= 31)
                    {
                        /*
                         * Even though SHORTABLE tick type supported only
                         * starting server version 33 it would be relatively
                         * expensive to expose this restriction here.
                         * 
                         * Therefore we are relying on TWS doing validation.
                         */

                        StringBuilder genList = new StringBuilder();
                        if (genericTickList != null)
                        {
                            for (int counter = 0; counter < genericTickList.Count; counter++)
                                genList.AppendFormat("{0},",
                                                     ((int) genericTickList[counter]).ToString(
                                                         CultureInfo.InvariantCulture));
                        }

                        if (marketDataOff)
                            genList.AppendFormat("mdoff");

                        send(genList.ToString().Trim(','));
                    }
                    //35 is the minum version for SnapShot
                    if (serverVersion >= 35)
                    {
                        send(snapshot);
                    }
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;
                    error(tickerId, ErrorMessage.FailSendRequestMarket, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call the CancelHistoricalData method to stop receiving historical data results.
        /// </summary>
        /// <param name="tickerId">the Id that was specified in the call to <see cref="RequestHistoricalData(int,Contract,DateTime,TimeSpan,BarSize,HistoricalDataType,int)"/>.</param>
        public void CancelHistoricalData(int tickerId)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                if (serverVersion < 24)
                {
                    error(ErrorMessage.UpdateTws, "It does not support historical data query cancellation.");
                    return;
                }

                int version = 1;

                // send cancel mkt data msg
                try
                {
                    send((int) OutgoingMessage.CancelHistoricalData);
                    send(version);
                    send(tickerId);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(tickerId, ErrorMessage.FailSendCancelHistoricalData, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call the CancelRealTimeBars() method to stop receiving real time bar results. 
        /// </summary>
        /// <param name="tickerId">The Id that was specified in the call to <see cref="RequestRealTimeBars"/>.</param>
        public void CancelRealTimeBars(int tickerId)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                //34 is the minimum server version for real time bars
                if (serverVersion < MinServerVersion.RealTimeBars)
                {
                    error(ErrorMessage.UpdateTws, "It does not support realtime bar data query cancellation.");
                    return;
                }

                int version = 1;

                // send cancel mkt data msg
                try
                {
                    send((int)OutgoingMessage.CancelRealTimeBars);
                    send(version);
                    send(tickerId);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) | throwExceptions)
                        throw;

                    error(tickerId, ErrorMessage.FailSendCancelRealTimeBars, e);
                    close();
                }
            }
        }
        
        /// <summary>
        /// Call the reqHistoricalData() method to start receiving historical data results through the historicalData() EWrapper method. 
        /// </summary>
        /// <param name="tickerId">the Id for the request. Must be a unique value. When the data is received, it will be identified by this Id. This is also used when canceling the historical data request.</param>
        /// <param name="contract">this structure contains a description of the contract for which market data is being requested.</param>
        /// <param name="endDateTime">Date is sent after a .ToUniversalTime, so make sure the kind property is set correctly, and assumes GMT timezone. Use the format yyyymmdd hh:mm:ss tmz, where the time zone is allowed (optionally) after a space at the end.</param>
        /// <param name="duration">This is the time span the request will cover, and is specified using the format:
        /// <integer /> <unit />, i.e., 1 D, where valid units are:
        /// S (seconds)
        /// D (days)
        /// W (weeks)
        /// M (months)
        /// Y (years)
        /// If no unit is specified, seconds are used. "years" is currently limited to one.
        /// </param>
        /// <param name="barSizeSetting">
        /// specifies the size of the bars that will be returned (within IB/TWS limits). Valid values include:
        /// <list type="table">
        /// <listheader>
        ///     <term>Bar Size</term>
        ///     <description>Parametric Value</description>
        /// </listheader>
        /// <item>
        ///     <term>1 sec</term>
        ///     <description>1</description>
        /// </item>
        /// <item>
        ///     <term>5 secs</term>
        ///     <description>2</description>
        /// </item>
        /// <item>
        ///     <term>15 secs</term>
        ///     <description>3</description>
        /// </item>
        /// <item>
        ///     <term>30 secs</term>
        ///     <description>4</description>
        /// </item>
        /// <item>
        ///     <term>1 min</term>
        ///     <description>5</description>
        /// </item>
        /// <item>
        ///     <term>2 mins</term>
        ///     <description>6</description>
        /// </item>
        /// <item>
        ///     <term>5 mins</term>
        ///     <description>7</description>
        /// </item>
        /// <item>
        ///     <term>15 mins</term>
        ///     <description>8</description>
        /// </item>
        /// <item>
        ///     <term>30 mins</term>
        ///     <description>9</description>
        /// </item>
        /// <item>
        ///     <term>1 hour</term>
        ///     <description>10</description>
        /// </item>
        /// <item>
        ///     <term>1 day</term>
        ///     <description>11</description>
        /// </item>
        /// <item>
        ///     <term>1 week</term>
        ///     <description></description>
        /// </item>
        /// <item>
        ///     <term>1 month</term>
        ///     <description></description>
        /// </item>
        /// <item>
        ///     <term>3 months</term>
        ///     <description></description>
        /// </item>
        /// <item>
        ///     <term>1 year</term>
        ///     <description></description>
        /// </item>
        /// </list>
        /// </param>
        /// <param name="whatToShow">determines the nature of data being extracted. Valid values include:
        /// TRADES
        /// MIDPOINT
        /// BID
        /// ASK
        /// BID/ASK
        /// </param>
        /// <param name="useRth">
        /// determines whether to return all data available during the requested time span, or only data that falls within regular trading hours. Valid values include:
        /// 0 - all data is returned even where the market in question was outside of its regular trading hours.
        /// 1 - only data within the regular trading hours is returned, even if the requested time span falls partially or completely outside of the RTH.
        /// </param>
        public void RequestHistoricalData(int tickerId, Contract contract, DateTime endDateTime, TimeSpan duration,
                                      BarSize barSizeSetting, HistoricalDataType whatToShow, int useRth)
        {
            DateTime beginDateTime = endDateTime.Subtract(duration);

            string dur = ConvertPeriodtoIb(beginDateTime, endDateTime);
            RequestHistoricalData(tickerId, contract, endDateTime, dur, barSizeSetting, whatToShow, useRth);
        }

        /// <summary>
        /// used for reqHistoricalData
        /// </summary>
        protected static string ConvertPeriodtoIb(DateTime StartTime, DateTime EndTime)
        {
            TimeSpan period = EndTime.Subtract(StartTime);
            double secs = period.TotalSeconds;
            long unit;

            if (secs < 1)
                throw new ArgumentOutOfRangeException("Period cannot be less than 1 second.");
            if (secs < 86400)
            {
                unit = (long) Math.Ceiling(secs);
                return string.Concat(unit, " S");
            }
            double days = secs/86400;

            unit = (long) Math.Ceiling(days);
            if (unit <= 34)
                return string.Concat(unit, " D");
            double weeks = days/7;
            unit = (long) Math.Ceiling(weeks);
            if (unit > 52)
                throw new ArgumentOutOfRangeException("Period cannot be bigger than 52 weeks.");
            return string.Concat(unit, " W");
        }

        /// <summary>
        /// Call the reqHistoricalData() method to start receiving historical data results through the historicalData() EWrapper method. 
        /// </summary>
        /// <param name="tickerId">the Id for the request. Must be a unique value. When the data is received, it will be identified by this Id. This is also used when canceling the historical data request.</param>
        /// <param name="contract">this structure contains a description of the contract for which market data is being requested.</param>
        /// <param name="endDateTime">Date is sent after a .ToUniversalTime, so make sure the kind property is set correctly, and assumes GMT timezone. Use the format yyyymmdd hh:mm:ss tmz, where the time zone is allowed (optionally) after a space at the end.</param>
        /// <param name="duration">This is the time span the request will cover, and is specified using the format:
        /// <integer /> <unit />, i.e., 1 D, where valid units are:
        /// S (seconds)
        /// D (days)
        /// W (weeks)
        /// M (months)
        /// Y (years)
        /// If no unit is specified, seconds are used. "years" is currently limited to one.
        /// </param>
        /// <param name="barSizeSetting">
        /// specifies the size of the bars that will be returned (within IB/TWS limits). Valid values include:
        /// <list type="table">
        /// <listheader>
        ///     <term>Bar Size</term>
        ///     <description>Parametric Value</description>
        /// </listheader>
        /// <item>
        ///     <term>1 sec</term>
        ///     <description>1</description>
        /// </item>
        /// <item>
        ///     <term>5 secs</term>
        ///     <description>2</description>
        /// </item>
        /// <item>
        ///     <term>15 secs</term>
        ///     <description>3</description>
        /// </item>
        /// <item>
        ///     <term>30 secs</term>
        ///     <description>4</description>
        /// </item>
        /// <item>
        ///     <term>1 min</term>
        ///     <description>5</description>
        /// </item>
        /// <item>
        ///     <term>2 mins</term>
        ///     <description>6</description>
        /// </item>
        /// <item>
        ///     <term>5 mins</term>
        ///     <description>7</description>
        /// </item>
        /// <item>
        ///     <term>15 mins</term>
        ///     <description>8</description>
        /// </item>
        /// <item>
        ///     <term>30 mins</term>
        ///     <description>9</description>
        /// </item>
        /// <item>
        ///     <term>1 hour</term>
        ///     <description>10</description>
        /// </item>
        /// <item>
        ///     <term>1 day</term>
        ///     <description>11</description>
        /// </item>
        /// <item>
        ///     <term>1 week</term>
        ///     <description></description>
        /// </item>
        /// <item>
        ///     <term>1 month</term>
        ///     <description></description>
        /// </item>
        /// <item>
        ///     <term>3 months</term>
        ///     <description></description>
        /// </item>
        /// <item>
        ///     <term>1 year</term>
        ///     <description></description>
        /// </item>
        /// </list>
        /// </param>
        /// <param name="whatToShow">determines the nature of data being extracted. Valid values include:
        /// TRADES
        /// MIDPOINT
        /// BID
        /// ASK
        /// BID/ASK
        /// </param>
        /// <param name="useRth">
        /// determines whether to return all data available during the requested time span, or only data that falls within regular trading hours. Valid values include:
        /// 0 - all data is returned even where the market in question was outside of its regular trading hours.
        /// 1 - only data within the regular trading hours is returned, even if the requested time span falls partially or completely outside of the RTH.
        /// </param>
        public void RequestHistoricalData(int tickerId, Contract contract, DateTime endDateTime, string duration,
                                      BarSize barSizeSetting, HistoricalDataType whatToShow, int useRth)
        {
            if (contract == null)
                throw new ArgumentNullException("contract");
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(tickerId, ErrorMessage.NotConnected);
                    return;
                }

                int version = 4;

                try
                {
                    if (serverVersion < 16)
                    {
                        error(ErrorMessage.UpdateTws, "It does not support historical data backfill.");
                        return;
                    }

                    send((int)OutgoingMessage.RequestHistoricalData);
                    send(version);
                    send(tickerId);

                    //Send Contract Fields
                    send(contract.Symbol);
                    send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                    send(contract.Expiry);
                    send(contract.Strike);
                    send(((contract.Right == RightType.Undefined)
                              ? ""
                              : EnumDescConverter.GetEnumDescription(contract.Right)));
                    send(contract.Multiplier);
                    send(contract.Exchange);
                    send(contract.PrimaryExchange);
                    send(contract.Currency);
                    send(contract.LocalSymbol);
                    if (serverVersion >= 31)
                    {
                        send(contract.IncludeExpired ? 1 : 0);
                    }
                    if (serverVersion >= 20)
                    {
                        //yyyymmdd hh:mm:ss tmz
                        send(endDateTime.ToUniversalTime().ToString("yyyyMMdd HH:mm:ss", CultureInfo.InvariantCulture) + " GMT");
                        send(EnumDescConverter.GetEnumDescription(barSizeSetting));
                    }
                    send(duration);
                    send(useRth);
                    send(EnumDescConverter.GetEnumDescription(whatToShow));
                    if (serverVersion > 16)
                    {
                        //Send date times as seconds since 1970
                        send(2);
                    }
                    if (contract.SecurityType == SecurityType.Bag)
                    {
                        if (contract.ComboLegs == null)
                        {
                            send(0);
                        }
                        else
                        {
                            send(contract.ComboLegs.Count);

                            ComboLeg comboLeg;
                            for (int i = 0; i < contract.ComboLegs.Count; i++)
                            {
                                comboLeg = (ComboLeg)contract.ComboLegs[i];
                                send(comboLeg.ConId);
                                send(comboLeg.Ratio);
                                send(EnumDescConverter.GetEnumDescription(comboLeg.Action));
                                send(comboLeg.Exchange);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;
                    error(tickerId, ErrorMessage.FailSendRequestHistoricalData, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this function to download all details for a particular underlying. the contract details will be received via the contractDetails() function on the EWrapper.
        /// </summary>
        /// <param name="requestId">Request Id for Contract Details</param>
        /// <param name="contract">summary description of the contract being looked up.</param>
        public void RequestContractDetails(int requestId, Contract contract)
        {
            if (contract == null)
                throw new ArgumentNullException("contract");
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                // This feature is only available for versions of TWS >=4
                if (serverVersion < 4)
                {
                    error(ErrorMessage.UpdateTws, "Does not support Request Contract Details.");
                    return;
                }

                if (serverVersion < MinServerVersion.SecIdType)
                {
                    if (contract.SecIdType != SecurityIdType.None || !string.IsNullOrEmpty(contract.SecId))
                    {
                        error(ErrorMessage.UpdateTws, "It does not support secIdType and secId parameters.");
                        return;
                    }
                }

                const int version = 6;

                try
                {
                    // send req mkt data msg
                    send((int) OutgoingMessage.RequestContractData);
                    send(version);

                    //MIN_SERVER_VER_CONTRACT_DATA_CHAIN = 40
                    if (serverVersion >= 40)
                    {
                        send(requestId);
                    }

                    if(serverVersion >= 37)
                    {
                        send(contract.ContractId);
                    }

                    send(contract.Symbol);
                    send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                    send(contract.Expiry);
                    send(contract.Strike);
                    send(EnumDescConverter.GetEnumDescription(contract.Right));
                    if (serverVersion >= 15)
                    {
                        send(contract.Multiplier);
                    }
                    send(contract.Exchange);
                    send(contract.Currency);
                    send(contract.LocalSymbol);
                    if (serverVersion >= 31)
                    {
                        send(contract.IncludeExpired);
                    }

                    if (serverVersion >= 45)
                    {
                        send(EnumDescConverter.GetEnumDescription(contract.SecIdType));
                        send(contract.SecId);
                    }
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;
                    error(ErrorMessage.FailSendRequestContract, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call the reqRealTimeBars() method to start receiving real time bar results through the realtimeBar() EWrapper method.
        /// </summary>
        /// <param name="tickerId">The Id for the request. Must be a unique value. When the data is received, it will be identified
        /// by this Id. This is also used when canceling the historical data request.</param>
        /// <param name="contract">This structure contains a description of the contract for which historical data is being requested.</param>
        /// <param name="barSize">Currently only 5 second bars are supported, if any other value is used, an exception will be thrown.</param>
        /// <param name="whatToShow">Determines the nature of the data extracted. Valid values include:
        /// TRADES
        /// BID
        /// ASK
        /// MIDPOINT
        /// </param>
        /// <param name="useRth">useRth – Regular Trading Hours only. Valid values include:
        /// 0 = all data available during the time span requested is returned, including time intervals when the market in question was outside of regular trading hours.
        /// 1 = only data within the regular trading hours for the product requested is returned, even if the time time span falls partially or completely outside.
        /// </param>
        public void RequestRealTimeBars(int tickerId, Contract contract, int barSize, RealTimeBarType whatToShow, bool useRth)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(tickerId, ErrorMessage.NotConnected);
                    return;
                }
                //34 is the minimum version for real time bars
                if (serverVersion < MinServerVersion.RealTimeBars)
                {
                    error(ErrorMessage.UpdateTws, "It does not support real time bars.");
                    return;
                }

                int version = 1;

                try
                {
                    // send req mkt data msg
                    send((int)OutgoingMessage.RequestRealTimeBars);
                    send(version);
                    send(tickerId);

                    //Send Contract Fields
                    send(contract.Symbol);
                    send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                    send(contract.Expiry);
                    send(contract.Strike);
                    send(EnumDescConverter.GetEnumDescription(contract.Right));
                    send(contract.Multiplier);
                    send(contract.Exchange);
                    send(contract.PrimaryExchange);
                    send(contract.Currency);
                    send(contract.LocalSymbol);
                    send(barSize);
                    send(EnumDescConverter.GetEnumDescription(whatToShow));
                    send(useRth);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;
                    error(tickerId, ErrorMessage.FailSendRequestRealTimeBars, e);
                    close();
                }
            }
        }
        

        /// <summary>
        /// Call this method to request market depth for a specific contract. The market depth will be returned by the updateMktDepth() and updateMktDepthL2() methods.
        /// </summary>
        /// <param name="tickerId">the ticker Id. Must be a unique value. When the market depth data returns, it will be identified by this tag. This is also used when canceling the market depth.</param>
        /// <param name="contract">this structure contains a description of the contract for which market depth data is being requested.</param>
        /// <param name="numberOfRows">specifies the number of market depth rows to return.</param>
        public void RequestMarketDepth(int tickerId, Contract contract, int numberOfRows)
        {
            if (contract == null)
                throw new ArgumentNullException("contract");
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                // This feature is only available for versions of TWS >=6
                if (serverVersion < 6)
                {
                    error(ErrorMessage.UpdateTws, "It does not support market depth.");
                    return;
                }

                int version = 3;

                try
                {
                    // send req mkt data msg
                    send((int) OutgoingMessage.RequestMarketDepth);
                    send(version);
                    send(tickerId);

                    //Request Contract Fields
                    send(contract.Symbol);
                    send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                    send(contract.Expiry);
                    send(contract.Strike);
                    send(((contract.Right == RightType.Undefined)
                              ? ""
                              : EnumDescConverter.GetEnumDescription(contract.Right)));
                    if (serverVersion >= 15)
                    {
                        send(contract.Multiplier);
                    }
                    send(contract.Exchange);
                    send(contract.Currency);
                    send(contract.LocalSymbol);
                    if (serverVersion >= 19)
                    {
                        send(numberOfRows);
                    }
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(tickerId, ErrorMessage.FailSendRequestMarketDepth, e);
                    close();
                }
            }
        }

        /// <summary>
        /// After calling this method, market data for the specified Id will stop flowing.
        /// </summary>
        /// <param name="tickerId">the Id that was specified in the call to reqMktData().</param>
        public void CancelMarketData(int tickerId)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                // send cancel mkt data msg
                try
                {
                    send((int) OutgoingMessage.CancelMarketData);
                    send(version);
                    send(tickerId);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(tickerId, ErrorMessage.FailSendCancelMarket, e);
                    close();
                }
            }
        }

        /// <summary>
        /// After calling this method, market depth data for the specified Id will stop flowing.
        /// </summary>
        /// <param name="tickerId">the Id that was specified in the call to reqMktDepth().</param>
        public void CancelMarketDepth(int tickerId)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                // This feature is only available for versions of TWS >=6
                if (serverVersion < 6)
                {
                    error(ErrorMessage.UpdateTws, "It does not support canceling market depth.");
                    return;
                }

                int version = 1;

                // send cancel mkt data msg
                try
                {
                    send((int) OutgoingMessage.CancelMarketDepth);
                    send(version);
                    send(tickerId);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(tickerId, ErrorMessage.FailSendCancelMarketDepth, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call the exerciseOptions() method to exercise options. 
        /// “SMART” is not an allowed exchange in exerciseOptions() calls, and that TWS does a moneyness request for the position in question whenever any API initiated exercise or lapse is attempted.
        /// </summary>
        /// <param name="tickerId">the Id for the exercise request.</param>
        /// <param name="contract">this structure contains a description of the contract to be exercised.  If no multiplier is specified, a default of 100 is assumed.</param>
        /// <param name="exerciseAction">this can have two values:
        /// 1 = specifies exercise
        /// 2 = specifies lapse
        /// </param>
        /// <param name="exerciseQuantity">the number of contracts to be exercised</param>
        /// <param name="account">specifies whether your setting will override the system's natural action. For example, if your action is "exercise" and the option is not in-the-money, by natural action the option would not exercise. If you have override set to "yes" the natural action would be overridden and the out-of-the money option would be exercised. Values are: 
        /// 0 = no
        /// 1 = yes
        /// </param>
        /// <param name="overrideRenamed">
        /// specifies whether your setting will override the system's natural action. For example, if your action is "exercise" and the option is not in-the-money, by natural action the option would not exercise. If you have override set to "yes" the natural action would be overridden and the out-of-the money option would be exercised. Values are: 
        /// 0 = no
        /// 1 = yes
        /// </param>
        public void ExerciseOptions(int tickerId, Contract contract, int exerciseAction, int exerciseQuantity,
                                    String account, int overrideRenamed)
        {
            if (contract == null)
                throw new ArgumentNullException("contract");
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(tickerId, ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                try
                {
                    if (serverVersion < 21)
                    {
                        error(ErrorMessage.UpdateTws, "It does not support options exercise from the API.");
                        return;
                    }

                    send((int) OutgoingMessage.ExerciseOptions);
                    send(version);
                    send(tickerId);
                    //Send Contract Fields
                    send(contract.Symbol);
                    send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                    send(contract.Expiry);
                    send(contract.Strike);
                    send(((contract.Right == RightType.Undefined)
                              ? ""
                              : EnumDescConverter.GetEnumDescription(contract.Right)));
                    send(contract.Multiplier);
                    send(contract.Exchange);
                    send(contract.Currency);
                    send(contract.LocalSymbol);
                    send(exerciseAction);
                    send(exerciseQuantity);
                    send(account);
                    send(overrideRenamed);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(tickerId, ErrorMessage.FailSendRequestMarket, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to place an order. The order status will be returned by the orderStatus event.
        /// </summary>
        /// <param name="orderId">the order Id. You must specify a unique value. When the order status returns, it will be identified by this tag. This tag is also used when canceling the order.</param>
        /// <param name="contract">this structure contains a description of the contract which is being traded.</param>
        /// <param name="order">this structure contains the details of the order.
        /// Each client MUST connect with a unique clientId.</param>
        public void PlaceOrder(int orderId, Contract contract, Order order)
        {
            if (contract == null)
                throw new ArgumentNullException("contract");
            if (order == null)
                throw new ArgumentNullException("order");
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(orderId, ErrorMessage.NotConnected);
                    return;
                }

                //Scale Orders Minimum Version is 35
                if (serverVersion < MinServerVersion.ScaleOrders)
                {
                    if (order.ScaleInitLevelSize != Int32.MaxValue || order.ScalePriceIncrement != Int32.MaxValue || order.ScalePriceIncrement != decimal.MaxValue)
                    {
                        error(orderId, ErrorMessage.UpdateTws, "It does not support Scale orders.");
                        return;
                    }
                }

                //Minimum Sell Short Combo Leg Order is 35
                if (serverVersion < MinServerVersion.SshortComboLegs)
                {
                    if (!(contract.ComboLegs.Count == 0))
                    {
                        ComboLeg comboLeg;
                        for (int i = 0; i < contract.ComboLegs.Count; ++i)
                        {
                            comboLeg = (ComboLeg)contract.ComboLegs[i];
                            if (comboLeg.ShortSaleSlot != 0 || (!string.IsNullOrEmpty(comboLeg.DesignatedLocation)))
                            {
                                error(orderId, ErrorMessage.UpdateTws, "It does not support SSHORT flag for combo legs.");
                                return;
                            }
                        }
                    }
                }

                if (serverVersion < MinServerVersion.WhatIfOrders)
                {
                    if(order.WhatIf)
                    {
                        error(orderId, ErrorMessage.UpdateTws, "It does not support what if orders.");
                        return;
                    }
                }

                if (serverVersion < MinServerVersion.FundamentalData)
                {
                    if (contract.UnderlyingComponent != null)
                    {
                        error(orderId, ErrorMessage.UpdateTws, "It does not support delta-neutral orders.");
                        return;
                    }
                }

                if (serverVersion < MinServerVersion.ScaleOrders2)
                {
                    if (order.ScaleSubsLevelSize != System.Int32.MaxValue)
                    {
                        error(orderId, ErrorMessage.UpdateTws, "It does not support Subsequent Level Size for Scale orders.");
                        return;
                    }
                }

                if (serverVersion < MinServerVersion.AlgoOrders)
                {
                    if (!string.IsNullOrEmpty(order.AlgoStrategy))
                    {
                        error(orderId, ErrorMessage.UpdateTws, "It does not support algo orders.");
                        return;
                    }
                }


                if (serverVersion < MinServerVersion.NotHeld)
                {
                    if (order.NotHeld)
                    {
                        error(ErrorMessage.UpdateTws, "It does not support notHeld parameter.");
                        return;
                    }
                }

                if (serverVersion < MinServerVersion.SecIdType)
                {
                    if (contract.SecIdType != SecurityIdType.None || !string.IsNullOrEmpty(contract.SecId))
                    {
                        error(ErrorMessage.UpdateTws, "It does not support secIdType and secId parameters.");
                        return;
                    }
                }

                if (serverVersion < MinServerVersion.PlaceOrderConId)
                {
                    if (contract.ContractId > 0)
                    {
                        error(ErrorMessage.UpdateTws, "It does not support conId parameter.");
                        return;
                    }
                }

                if (serverVersion < MinServerVersion.Sshortx)
                {
                    if (order.ExemptCode != -1)
                    {
                        error(ErrorMessage.UpdateTws, "It does not support exemptCode parameter.");
                        return;
                    }
                }

                if (serverVersion < MinServerVersion.Sshortx)
                {
                    if (contract.ComboLegs.Count > 0)
                    {
                        foreach(var comboLeg in contract.ComboLegs)
                        {
                            if (comboLeg.ExemptCode != -1)
                            {
                                error(ErrorMessage.UpdateTws, "It does not support exemptCode parameter.");
                                return;
                            }
                        }
                    }
                }

                if (serverVersion < MinServerVersion.HedgeOrders)
                {
                    if (!string.IsNullOrEmpty(order.HedgeType))
                    {
                        error(ErrorMessage.UpdateTws, "It does not support hedge orders.");
                        return;
                    }
                }

                if (serverVersion < MinServerVersion.OptOutSmartRouting)
                {
                    if (order.OptOutSmartRouting)
                    {
                        error(ErrorMessage.UpdateTws, "It does not support optOutSmartRouting parameter.");
                        return;
                    }
                }

                if (serverVersion < MinServerVersion.DeltaNeutralConId)
                {
                    if (order.DeltaNeutralConId > 0
                            || !string.IsNullOrEmpty(order.DeltaNeutralSettlingFirm)
                            || !string.IsNullOrEmpty(order.DeltaNeutralClearingAccount)
                            || !string.IsNullOrEmpty(order.DeltaNeutralClearingIntent)
                            )
                    {
                        error(ErrorMessage.UpdateTws, "It does not support deltaNeutral parameters: ConId, SettlingFirm, ClearingAccount, ClearingIntent");
                        return;
                    }
                }

                int version = (serverVersion < MinServerVersion.NotHeld) ? 27 : 35;

                // send place order msg
                try
                {
                    send((int) OutgoingMessage.PlaceOrder);
                    send(version);
                    send(orderId);

                    // send contract fields
                    if (serverVersion >= 46)
                        send(contract.ContractId);
                    send(contract.Symbol);
                    send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                    send(contract.Expiry);
                    send(contract.Strike);
                    send(((contract.Right == RightType.Undefined)
                              ? ""
                              : EnumDescConverter.GetEnumDescription(contract.Right)));
                    if (serverVersion >= 15)
                    {
                        send(contract.Multiplier);
                    }
                    send(contract.Exchange);
                    if (serverVersion >= 14)
                    {
                        send(contract.PrimaryExchange);
                    }
                    send(contract.Currency);
                    if (serverVersion >= 2)
                    {
                        send(contract.LocalSymbol);
                    }
                    if (serverVersion >= 45)
                    {
                        send(EnumDescConverter.GetEnumDescription(contract.SecIdType));
                        send(contract.SecId);
                    }

                    // send main order fields
                    send(EnumDescConverter.GetEnumDescription(order.Action));
                    send(order.TotalQuantity);
                    send(EnumDescConverter.GetEnumDescription(order.OrderType));
                    send(order.LimitPrice);
                    send(order.AuxPrice);

                    // send extended order fields
                    send(EnumDescConverter.GetEnumDescription(order.Tif));
                    send(order.OcaGroup);
                    send(order.Account);
                    send(order.OpenClose);
                    send((int) order.Origin);
                    send(order.OrderRef);
                    send(order.Transmit);
                    if (serverVersion >= 4)
                    {
                        send(order.ParentId);
                    }

                    if (serverVersion >= 5)
                    {
                        send(order.BlockOrder);
                        send(order.SweepToFill);
                        send(order.DisplaySize);
                        send((int)order.TriggerMethod);
                        if(serverVersion < 38)
                        {
                            //will never happen
                            send(false);
                        }
                        else
                        {
                            send(order.OutsideRth);
                        }
                    }

                    if (serverVersion >= 7)
                    {
                        send(order.Hidden);
                    }

                    // Send combo legs for BAG requests
                    if (serverVersion >= 8 && contract.SecurityType == SecurityType.Bag)
                    {
                        if (contract.ComboLegs == null)
                        {
                            send(0);
                        }
                        else
                        {
                            send(contract.ComboLegs.Count);

                            ComboLeg comboLeg;
                            for (int i = 0; i < contract.ComboLegs.Count; i++)
                            {
                                comboLeg = (ComboLeg) contract.ComboLegs[i];
                                send(comboLeg.ConId);
                                send(comboLeg.Ratio);
                                send(EnumDescConverter.GetEnumDescription(comboLeg.Action));
                                send(comboLeg.Exchange);
                                send((int)comboLeg.OpenClose);
                                //Min Combo Leg Short Sale Server Version is 35
                                if (serverVersion >= 35)
                                {
                                    send((int)comboLeg.ShortSaleSlot);
                                    send(comboLeg.DesignatedLocation);
                                }
                                if (serverVersion >= 51)
                                    send(comboLeg.ExemptCode);
                            }
                        }
                    }

                    if (serverVersion >= MinServerVersion.SmartComboRoutingParams && contract.SecurityType == SecurityType.Bag)
                    {
                        Collection<TagValue> smartComboRoutingParams = order.SmartComboRoutingParams;
                        int smartComboRoutingParamsCount = smartComboRoutingParams == null ? 0 : smartComboRoutingParams.Count;
                        send(smartComboRoutingParamsCount);
                        if (smartComboRoutingParamsCount > 0)
                        {
                            for (int i = 0; i < smartComboRoutingParamsCount; ++i)
                            {
                                TagValue tagValue = (TagValue)smartComboRoutingParams[i];
                                send(tagValue.Tag);
                                send(tagValue.Value);
                            }
                        }
                    }

                    if (serverVersion >= 9)
                    {
                        send("");
                    }

                    if (serverVersion >= 10)
                    {
                        send(order.DiscretionaryAmt);
                    }

                    if (serverVersion >= 11)
                    {
                        send(order.GoodAfterTime);
                    }

                    if (serverVersion >= 12)
                    {
                        send(order.GoodTillDate);
                    }

                    if (serverVersion >= 13)
                    {
                        send(order.FAGroup);
                        send(EnumDescConverter.GetEnumDescription(order.FAMethod));
                        send(order.FAPercentage);
                        send(order.FAProfile);
                    }
                    if (serverVersion >= 18)
                    {
                        // institutional short sale slot fields.
                        send((int)order.ShortSaleSlot); // 0 only for retail, 1 or 2 only for institution.
                        send(order.DesignatedLocation); // only populate when order.shortSaleSlot = 2.
                    }

                    if (serverVersion >= 51)
                        send(order.ExemptCode);

                    if (serverVersion >= 19)
                    {
                        send((int)order.OcaType);
                        if(serverVersion < 38)
                        {
                            //will never happen
                            send(false);
                        }
                        send(EnumDescConverter.GetEnumDescription(order.Rule80A));
                        send(order.SettlingFirm);
                        send(order.AllOrNone);
                        sendMax(order.MinQty);
                        sendMax(order.PercentOffset);
                        send(order.ETradeOnly);
                        send(order.FirmQuoteOnly);
                        sendMax(order.NbboPriceCap);
                        sendMax((int) order.AuctionStrategy);
                        sendMax(order.StartingPrice);
                        sendMax(order.StockRefPrice);
                        sendMax(order.Delta);
                        // Volatility orders had specific watermark price attribs in server version 26
                        double lower = (serverVersion == 26 && order.OrderType.Equals(OrderType.Volatility))
                                           ? Double.MaxValue
                                           : order.StockRangeLower;
                        double upper = (serverVersion == 26 && order.OrderType.Equals(OrderType.Volatility))
                                           ? Double.MaxValue
                                           : order.StockRangeUpper;
                        sendMax(lower);
                        sendMax(upper);
                    }

                    if (serverVersion >= 22)
                    {
                        send(order.OverridePercentageConstraints);
                    }

                    if (serverVersion >= 26)
                    {
                        // Volatility orders
                        sendMax(order.Volatility);
                        sendMax((int) order.VolatilityType);
                        if (serverVersion < 28)
                        {
                            send(order.DeltaNeutralOrderType.Equals(OrderType.Market));
                        }
                        else
                        {
                            send(EnumDescConverter.GetEnumDescription(order.DeltaNeutralOrderType));
                            sendMax(order.DeltaNeutralAuxPrice);

                            if (serverVersion >= MinServerVersion.DeltaNeutralConId && order.DeltaNeutralOrderType != OrderType.Empty)
                            {
                                send(order.DeltaNeutralConId);
                                send(order.DeltaNeutralSettlingFirm);
                                send(order.DeltaNeutralClearingAccount);
                                send(order.DeltaNeutralClearingIntent);
                            }
                        }
                        send(order.ContinuousUpdate);
                        if (serverVersion == 26)
                        {
                            // Volatility orders had specific watermark price attribs in server version 26
                            double lower = order.OrderType.Equals(OrderType.Volatility)
                                               ? order.StockRangeLower
                                               : Double.MaxValue;
                            double upper = order.OrderType.Equals(OrderType.Volatility)
                                               ? order.StockRangeUpper
                                               : Double.MaxValue;
                            sendMax(lower);
                            sendMax(upper);
                        }
                        sendMax(order.ReferencePriceType);
                    }

                    if (serverVersion >= 30)
                    {
                        // TRAIL_STOP_LIMIT stop price
                        sendMax(order.TrailStopPrice);
                    }

                    //Scale Orders require server version 35 or higher.
                    if (serverVersion >= MinServerVersion.ScaleOrders)
                    {
                        if (serverVersion >= MinServerVersion.ScaleOrders2)
                        {
                            sendMax(order.ScaleInitLevelSize);
                            sendMax(order.ScaleSubsLevelSize);
                        }
                        else
                        {
                            send("");
                            sendMax(order.ScaleInitLevelSize);
                        }
                        sendMax(order.ScalePriceIncrement);
                    }

                    if (serverVersion >= MinServerVersion.HedgeOrders)
                    {
                        send(order.HedgeType);
                        if (!string.IsNullOrEmpty(order.HedgeType))
                        {
                            send(order.HedgeParam);
                        }
                    }

                    if (serverVersion >= MinServerVersion.OptOutSmartRouting)
                    {
                        send(order.OptOutSmartRouting);
                    }

                    if(serverVersion >= MinServerVersion.PtaOrders)
                    {
                        send(order.ClearingAccount);
                        send(order.ClearingIntent);
                    }

                    if(serverVersion >= MinServerVersion.NotHeld)
                        send(order.NotHeld);

                    if (serverVersion >= MinServerVersion.UnderComp)
                    {
                        if (contract.UnderlyingComponent != null)
                        {
                            UnderlyingComponent underComp = contract.UnderlyingComponent;
                            send(true);
                            send(underComp.ContractId);
                            send(underComp.Delta);
                            send(underComp.Price);
                        }
                        else
                        {
                            send(false);
                        }
                    }

                    if (serverVersion >= MinServerVersion.AlgoOrders)
                    {
                        send(order.AlgoStrategy);
                        if (!string.IsNullOrEmpty(order.AlgoStrategy))
                        {
                            if(order.AlgoParams == null)
                            {
                                send(0);
                            }
                            else
                            {
                                send(order.AlgoParams.Count);
                                foreach(TagValue tagValue in order.AlgoParams)
                                {
                                    send(tagValue.Tag);
                                    send(tagValue.Value);
                                }
                            }
                        }
                    }

                    if(serverVersion >= MinServerVersion.WhatIfOrders)
                    {
                        send(order.WhatIf);
                    }
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(orderId, ErrorMessage.FailSendOrder, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this function to start getting account values, portfolio, and last update time information.
        /// </summary>
        /// <param name="subscribe">If set to TRUE, the client will start receiving account and portfolio updates. If set to FALSE, the client will stop receiving this information.</param>
        /// <param name="acctCode">the account code for which to receive account and portfolio updates.</param>
        public void RequestAccountUpdates(bool subscribe, String acctCode)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 2;

                // send cancel order msg
                try
                {
                    send((int) OutgoingMessage.RequestAccountData);
                    send(version);
                    send(subscribe);

                    // Send the account code. This will only be used for FA clients
                    if (serverVersion >= 9)
                    {
                        send(acctCode);
                    }
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendAccountUpdate, e);
                    close();
                }
            }
        }

        /// <summary>
        /// When this method is called, the execution reports that meet the filter criteria are downloaded to the client via the execDetails() method.
        /// </summary>
        /// <param name="requestId">Id of the request</param>
        /// <param name="filter">the filter criteria used to determine which execution reports are returned.</param>
        public void RequestExecutions(int requestId, ExecutionFilter filter)
        {
            if (filter == null)
                filter = new ExecutionFilter(0, "", DateTime.MinValue, "", SecurityType.Undefined, "", ActionSide.Undefined);
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 3;

                // send RequestExecutions msg
                try
                {
                    send((int) OutgoingMessage.RequestExecutions);
                    send(version);

                    if (serverVersion >= 42)
                    {
                        send(requestId);
                    }

                    // Send the execution rpt filter data
                    if (serverVersion >= 9)
                    {
                        send(filter.ClientId);
                        send(filter.AcctCode);

                        // The valid format for time is "yyyymmdd-hh:mm:ss"
                        send(filter.Time.ToUniversalTime().ToString("yyyyMMdd-HH:mm:ss", CultureInfo.InvariantCulture));
                        send(filter.Symbol);
                        send(EnumDescConverter.GetEnumDescription(filter.SecurityType));
                        send(filter.Exchange);
                        send(EnumDescConverter.GetEnumDescription(filter.Side));
                    }
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;
                    error(ErrorMessage.FailSendExecution, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to cancel an order.
        /// </summary>
        /// <param name="orderId">Call this method to cancel an order.</param>
        public void CancelOrder(int orderId)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(orderId, ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                // send cancel order msg
                try
                {
                    send((int) OutgoingMessage.CancelOrder);
                    send(version);
                    send(orderId);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;
                    error(orderId, ErrorMessage.FailSendCancelOrder, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to request the open orders that were placed from this client. Each open order will be fed back through the openOrder() and orderStatus() functions on the EWrapper.
        /// 
        /// The client with a clientId of "0" will also receive the TWS-owned open orders. These orders will be associated with the client and a new orderId will be generated. This association will persist over multiple API and TWS sessions.
        /// </summary>
        public void RequestOpenOrders()
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                // send cancel order msg
                try
                {
                    send((int) OutgoingMessage.RequestOpenOrders);
                    send(version);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendOpenOrder, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Returns one next valid Id...
        /// </summary>
        /// <param name="numberOfIds">Has No Effect</param>
        public void RequestIds(int numberOfIds)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                try
                {
                    send((int) OutgoingMessage.RequestIds);
                    send(version);
                    send(numberOfIds);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendCancelOrder, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to start receiving news bulletins. Each bulletin will be returned by the updateNewsBulletin() method.
        /// </summary>
        /// <param name="allMessages">if set to TRUE, returns all the existing bulletins for the current day and any new ones. IF set to FALSE, will only return new bulletins.</param>
        public void RequestNewsBulletins(bool allMessages)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                try
                {
                    send((int) OutgoingMessage.RequestNewsBulletins);
                    send(version);
                    send(allMessages);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendCancelOrder, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to stop receiving news bulletins.
        /// </summary>
        public void CancelNewsBulletins()
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                // send cancel order msg
                try
                {
                    send((int) OutgoingMessage.CancelNewsBulletins);
                    send(version);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendCancelOrder, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to request that newly created TWS orders be implicitly associated with the client. When a new TWS order is created, the order will be associated with the client and fed back through the openOrder() and orderStatus() methods on the EWrapper.
        /// 
        /// TWS orders can only be bound to clients with a clientId of “0”.
        /// </summary>
        /// <param name="autoBind">If set to TRUE, newly created TWS orders will be implicitly associated with the client. If set to FALSE, no association will be made.</param>
        public void RequestAutoOpenOrders(bool autoBind)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                // send req open orders msg
                try
                {
                    send((int) OutgoingMessage.RequestAutoOpenOrders);
                    send(version);
                    send(autoBind);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendOpenOrder, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to request the open orders that were placed from all clients and also from TWS. Each open order will be fed back through the openOrder() and orderStatus() functions on the EWrapper.
        /// 
        /// No association is made between the returned orders and the requesting client.
        /// </summary>
        public void RequestAllOpenOrders()
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                // send req all open orders msg
                try
                {
                    send((int) OutgoingMessage.RequestAllOpenOrders);
                    send(version);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendOpenOrder, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to request the list of managed accounts. The list will be returned by the managedAccounts() function on the EWrapper.
        /// 
        /// This request can only be made when connected to a Financial Advisor (FA) account.
        /// </summary>
        public void RequestManagedAccts()
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                int version = 1;

                // send req FA managed accounts msg
                try
                {
                    send((int) OutgoingMessage.RequestManagedAccounts);
                    send(version);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendOpenOrder, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to request FA configuration information from TWS. The data returns in an XML string via the receiveFA() method.
        /// </summary>
        /// <param name="faDataType">
        /// faDataType - specifies the type of Financial Advisor configuration data being requested. Valid values include:
        /// 1 = GROUPS
        /// 2 = PROFILE
        /// 3 =ACCOUNT ALIASES
        /// </param>
        public void RequestFA(FADataType faDataType)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                // This feature is only available for versions of TWS >= 13
                if (serverVersion < 13)
                {
                    error(ErrorMessage.UpdateTws, "Does not support request FA.");
                    return;
                }

                int version = 1;

                try
                {
                    send((int) OutgoingMessage.RequestFA);
                    send(version);
                    send((int) faDataType);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendFARequest, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to request FA configuration information from TWS. The data returns in an XML string via a "receiveFA" ActiveX event.  
        /// </summary>
        /// <param name="faDataType">
        /// specifies the type of Financial Advisor configuration data being requested. Valid values include:
        /// 1 = GROUPS
        /// 2 = PROFILE
        /// 3 = ACCOUNT ALIASES</param>
        /// <param name="xml">the XML string containing the new FA configuration information.</param>
        public void ReplaceFA(FADataType faDataType, String xml)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                // This feature is only available for versions of TWS >= 13
                if (serverVersion < 13)
                {
                    error(ErrorMessage.UpdateTws, "Does not support Replace FA.");
                    return;
                }

                int version = 1;

                try
                {
                    send((int) OutgoingMessage.ReplaceFA);
                    send(version);
                    send((int) faDataType);
                    send(xml);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendFAReplace, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Returns the current system time on the server side.
        /// </summary>
        public void RequestCurrentTime()
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected);
                    return;
                }

                // This feature is only available for versions of TWS >= 33
                if (serverVersion < 33)
                {
                    error(ErrorMessage.UpdateTws, "It does not support current time requests.");
                    return;
                }

                int version = 1;

                try
                {
                    send((int)OutgoingMessage.RequestCurrentTime);
                    send(version);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendRequestCurrentTime, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Request Fundamental Data
        /// </summary>
        /// <param name="requestId">Request Id</param>
        /// <param name="contract">Contract to request fundamental data for</param>
        /// <param name="reportType">Report Type</param>
        public virtual void RequestFundamentalData(int requestId, Contract contract, String reportType)
        {
            lock (this)
            {
                if (!connected)
                {
                    error(requestId, ErrorMessage.NotConnected);
                    return;
                }

                if (serverVersion < MinServerVersion.FundamentalData)
                {
                    error(requestId, ErrorMessage.UpdateTws, "It does not support fundamental data requests.");
                    return;
                }

                int version = 1;

                try
                {
                    // send req fund data msg
                    send((int)OutgoingMessage.RequestFundamentalData);
                    send(version);
                    send(requestId);

                    // send contract fields
                    send(contract.Symbol);
                    send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                    send(contract.Exchange);
                    send(contract.PrimaryExchange);
                    send(contract.Currency);
                    send(contract.LocalSymbol);

                    send(reportType);
                }
                catch (Exception e)
                {
                    error(requestId, ErrorMessage.FailSendRequestFundData, "" + e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this method to stop receiving Reuters global fundamental data.
        /// </summary>
        /// <param name="requestId">The ID of the data request.</param>
        public virtual void CancelFundamentalData(int requestId)
        {
            lock (this)
            {
                if (!connected)
                {
                    error(requestId, ErrorMessage.NotConnected);
                    return;
                }

                if (serverVersion < MinServerVersion.FundamentalData)
                {
                    error(requestId, ErrorMessage.UpdateTws, "It does not support fundamental data requests.");
                    return;
                }

                int version = 1;

                try
                {
                    // send req mkt data msg
                    send((int)OutgoingMessage.CancelFundamentalData);
                    send(version);
                    send(requestId);
                }
                catch (Exception e)
                {
                    error(requestId, ErrorMessage.FailSendCancelFundData, "" + e);
                    close();
                }
            }
        }
        
        /// <summary>
        /// Call this function to cancel a request to calculate volatility for a supplied option price and underlying price.
        /// </summary>
        /// <param name="reqId">The Ticker Id.</param>
        public virtual void CancelCalculateImpliedVolatility(int reqId)
        {
            if (!connected)
            {
                error(ErrorMessage.NotConnected);
                return;
            }

            if (serverVersion < MinServerVersion.CancelCalculateImpliedVolatility)
            {
                error(reqId, ErrorMessage.UpdateTws, "It does not support calculate implied volatility cancellation.");
                return;
            }

            const int version = 1;

            try
            {
                // send cancel calculate implied volatility msg
                send((int)OutgoingMessage.CancelCalcImpliedVolatility);
                send(version);
                send(reqId);
            }
            catch (Exception e)
            {
                error(reqId, ErrorMessage.FailSendCancelCalculateImpliedVolatility, e);
                close();
            }
        }

        /// <summary>
        /// Calculates the Implied Volatility based on the user-supplied option and underlying prices.
        /// The calculated implied volatility is returned by tickOptionComputation( ) in a new tick type, CUST_OPTION_COMPUTATION, which is described below.
        /// </summary>
        /// <param name="requestId">Request Id</param>
        /// <param name="contract">Contract</param>
        /// <param name="optionPrice">Price of the option</param>
        /// <param name="underPrice">Price of teh underlying of the option</param>
        public virtual void RequestCalculateImpliedVolatility(int requestId, Contract contract, double optionPrice, double underPrice)
        {    
            lock(this)
            {
                if (!connected)
                {
                    error(requestId, ErrorMessage.NotConnected);
                    return;
                }

                if (serverVersion < MinServerVersion.RequestCalculateImpliedVolatility)
                {
                    error(ErrorMessage.UpdateTws, "It does not support calculate implied volatility requests.");
                    return;
                }

                const int version = 1;

                try
                {
                    // send calculate implied volatility msg
                    send((int)OutgoingMessage.RequestCalcImpliedVolatility);
                    send(version);
                    send(requestId);

                    // send contract fields
                    send(contract.ContractId);
                    send(contract.Symbol);
                    send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                    send(contract.Expiry);
                    send(contract.Strike);
                    send(((contract.Right == RightType.Undefined)
                              ? ""
                              : EnumDescConverter.GetEnumDescription(contract.Right)));
                    send(contract.Multiplier);
                    send(contract.Exchange);
                    send(contract.PrimaryExchange);
                    send(contract.Currency);
                    send(contract.LocalSymbol);

                    send(optionPrice);
                    send(underPrice);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(requestId, ErrorMessage.FailSendReqCalcImpliedVolatility, e);
                    close();
                }
            }
        }

        /// <summary>
        /// Call this function to calculate option price and greek values for a supplied volatility and underlying price.
        /// </summary>
        /// <param name="reqId">The ticker ID.</param>
        /// <param name="contract">Describes the contract.</param>
        /// <param name="volatility">The volatility.</param>
        /// <param name="underPrice">Price of the underlying.</param>
        public virtual void RequestCalculateOptionPrice(int reqId, Contract contract, double volatility,
                                                        double underPrice)
        {
            if (!connected)
            {
                error(ErrorMessage.NotConnected);
                return;
            }

            if (serverVersion < MinServerVersion.RequestCalculateOptionPrice)
            {
                error(reqId, ErrorMessage.UpdateTws, "It does not support calculate option price requests.");
                return;
            }

            const int version = 1;

            try
            {
                // send calculate option price msg
                send((int)OutgoingMessage.RequestCalcOptionPrice);
                send(version);
                send(reqId);

                // send contract fields
                send(contract.ContractId);
                send(contract.Symbol);
                send(EnumDescConverter.GetEnumDescription(contract.SecurityType));
                send(contract.Expiry);
                send(contract.Strike);
                send(EnumDescConverter.GetEnumDescription(contract.Right));
                send(contract.Multiplier);
                send(contract.Exchange);
                send(contract.PrimaryExchange);
                send(contract.Currency);
                send(contract.LocalSymbol);

                send(volatility);
                send(underPrice);
            }
            catch (Exception e)
            {
                error(reqId, ErrorMessage.FailSendRequestCalcOptionPrice, e);
                close();
            }
        }

        /// <summary>
        /// Call this function to cancel a request to calculate option price and greek values for a supplied volatility and underlying price.
        /// </summary>
        /// <param name="reqId">The ticker id.</param>
        public virtual void CancelCalculateOptionPrice(int reqId)
        {
            if (!connected)
            {
                error(ErrorMessage.NotConnected);
                return;
            }

            if (serverVersion < MinServerVersion.CancelCalculateOptionPrice)
            {
                error(reqId, ErrorMessage.UpdateTws, "It does not support calculate option price cancellation.");
                return;
            }

            const int version = 1;

            try
            {
                // send cancel calculate option price msg
                send((int)OutgoingMessage.CancelCalcOptionPrice);
                send(version);
                send(reqId);
            }
            catch (Exception e)
            {
                error(reqId, ErrorMessage.FailSendCancelCalculateOptionPrice, e);
                close();
            }
        }

        /// <summary>
        /// Request Global Cancel.
        /// </summary>
        public virtual void RequestGlobalCancel()
        {
            // not connected?
            if (!connected)
            {
                error(ErrorMessage.NotConnected);
                return;
            }

            if (serverVersion < MinServerVersion.RequestGlobalCancel)
            {
                error(ErrorMessage.UpdateTws, "It does not support globalCancel requests.");
                return;
            }

            const int version = 1;

            // send request global cancel msg
            try
            {
                send((int)OutgoingMessage.RequestGlobalCancel);
                send(version);
            }
            catch (Exception e)
            {
                error(ErrorMessage.FailSendRequestGlobalCancel, e);
                close();
            }
        }

        /// <summary>
        /// The API can receive frozen market data from Trader Workstation. 
        /// Frozen market data is the last data recorded in our system. 
        /// During normal trading hours, the API receives real-time market data. 
        /// If you use this function, you are telling TWS to automatically switch to frozen market data after the close. 
        /// Then, before the opening of the next trading day, market data will automatically switch back to real-time market data.
        /// </summary>
        /// <param name="marketDataType">1 for real-time streaming market data or 2 for frozen market data.</param>
        public virtual void RequestMarketDataType(int marketDataType)
        {
            // not connected?
            if (!connected)
            {
                error(ErrorMessage.NotConnected);
                return;
            }

            if (serverVersion < MinServerVersion.RequestMarketDataType)
            {
                error(ErrorMessage.UpdateTws, "It does not support marketDataType requests.");
                return;
            }

            const int version = 1;

            // send the reqMarketDataType message
            try
            {
                send((int)OutgoingMessage.RequestMarketDataType);
                send(version);
                send(marketDataType);
            }
            catch (Exception e)
            {
                error(ErrorMessage.FailSendRequestMarketDataType, e);
                close();
            }
        }

        /// <summary>
        /// The default level is ERROR. Refer to the API logging page for more details.
        /// </summary>
        /// <param name="serverLogLevel">
        /// logLevel - specifies the level of log entry detail used by the server (TWS) when processing API requests. Valid values include: 
        /// 1 = SYSTEM
        /// 2 = ERROR
        /// 3 = WARNING
        /// 4 = INFORMATION
        /// 5 = DETAIL
        /// </param>
        public void SetServerLogLevel(LogLevel serverLogLevel)
        {
            lock (this)
            {
                // not connected?
                if (!connected)
                {
                    error(ErrorMessage.NotConnected, "");
                    return;
                }

                int version = 1;

                // send the set server logging level message
                try
                {
                    send((int) OutgoingMessage.SetServerLogLevel);
                    send(version);
                    send((int) serverLogLevel);
                }
                catch (Exception e)
                {
                    if (!(e is ObjectDisposedException || e is IOException) || throwExceptions)
                        throw;

                    error(ErrorMessage.FailSendServerLogLevel, e.ToString());
                    close();
                }
            }
        }

        #endregion

        #region Helper Methods

        private void send(String str)
        {
            // write string to data buffer; writer thread will
            // write it to ibSocket
            if (!string.IsNullOrEmpty(str))
            {
                dos.Write(ToByteArray(str));
            }
            sendEOL();
        }

        /// <summary>
        /// Converts a string to an array of bytes
        /// </summary>
        /// <param name="source">The string to be converted</param>
        /// <returns>The new array of bytes</returns>
        private static byte[] ToByteArray(String source)
        {
            return UTF8Encoding.UTF8.GetBytes(source);
        }

        private void sendEOL()
        {
            dos.Write(EOL);
        }

        private void send(int val)
        {
            send(Convert.ToString(val, CultureInfo.InvariantCulture));
        }


        private void send(double val)
        {
            send(Convert.ToString(val, CultureInfo.InvariantCulture));
        }

        private void send(decimal val)
        {
            send(Convert.ToString(val, CultureInfo.InvariantCulture));
        }

        private void sendMax(double val)
        {
            if (val == Double.MaxValue)
            {
                sendEOL();
            }
            else
            {
                send(Convert.ToString(val, CultureInfo.InvariantCulture));
            }
        }

        private void sendMax(int val)
        {
            if (val == Int32.MaxValue)
            {
                sendEOL();
            }
            else
            {
                send(Convert.ToString(val, CultureInfo.InvariantCulture));
            }
        }

        private void sendMax(decimal val)
        {
            if (val == decimal.MaxValue)
            {
                sendEOL();
            }
            else
            {
                send(Convert.ToString(val, CultureInfo.InvariantCulture));
            }
        }

        private void send(bool val)
        {
            send(val ? 1 : 0);
        }

        private void send(bool? val)
        {
            if(val!=null)
            {
                send(val.Value);
            }
            else
            {
                send("");
            }
        }

        #endregion

    }
}
