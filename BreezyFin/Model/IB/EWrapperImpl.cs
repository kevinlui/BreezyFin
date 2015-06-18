using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows.Threading;


using Breezy.Fin.Model.Factories;
using Breezy.Fin.Model;
using Breezy.Fin.ViewModel;
using Breezy.Fin.Model.IB.MarketData;


using IBApi;
using ReactiveUI;

namespace Breezy.Fin.Model.IB
{
    public class EWrapperImpl : EWrapper
    {
        public EClientSocket ClientSocket;
        //public ObservableCollection<Asset> Positions = new ObservableCollection<Asset>();
        public ObservableConcurrentDictionary<string, Asset> DictPositions = new ObservableConcurrentDictionary<string, Asset>();
        private Dispatcher mainThreadDispatcher;

        private int tickerID = 0;
        private Dictionary<int, string> dictTicker2Symbol = new Dictionary<int, string>();

        private int nextOrderID;

        public EWrapperImpl(Dispatcher mainThreadDispatcher)
        {
            ClientSocket = new EClientSocket(this);

            this.mainThreadDispatcher = mainThreadDispatcher;
        }

        /** @brief Handles errors generated within the API itself.
         * If an exception is thrown within the API code it will be notified here. Posible cases include errors while reading the information from the socket or even misshandlings at EWrapper's implementing class.
         * @param e the thrown exception.
         */
        void EWrapper.error(Exception e)
        {
            Console.WriteLine(string.Format("Error: Exception.Message={0}", e.Message));
        }

        /**
         * @param str The error message received.
         * 
         */
        void EWrapper.error(string str)
        {
            Console.WriteLine(string.Format("Error: str={0}", str));
        }

        /**
         * @brief Errors sent by the TWS are received here.
         * @param id the request identifier which generated the error.
         * @param errorCode the code identifying the error.
         * @param errorMsg error's description.
         *  
         */
        void EWrapper.error(int id, int errorCode, string errorMsg)
        {
            Console.WriteLine(string.Format("Error: id={0}, errorCode={1}, errorMsg={2}", id, errorCode, errorMsg));
        }

        /**
         * @brief Server's current time
         * This method will receive IB server's system time resulting after the invokation of reqCurrentTime
         * @sa reqCurrentTime()
         */
        void EWrapper.currentTime(long time)
        {
            Console.WriteLine(string.Format("currentTime: time={0}", time));
        }

        /**
         * @brief Market data tick price callback.
         * Handles all price related ticks.
         * @param tickerId the request's unique identifier.
         * @param field the type of the price being received (i.e. ask price).
         * @param price the actual price.
         * @param canAutoExecute Specifies whether the price tick is available for automatic execution (1) or not (0).
         * @sa TickType, tickSize, tickString, tickEFP, tickGeneric, tickOptionComputation, tickSnapshotEnd, marketDataType, EClientSocket::reqMktData
         */
        void EWrapper.tickPrice(int tickerId, int field, double price, int canAutoExecute)
        {
            TickType tickType = (TickType)field;
            if (price > 0 && (tickType == TickType.LastPrice || tickType == TickType.ClosePrice))
            {
                var symbol = (dictTicker2Symbol.ContainsKey(tickerId) ? dictTicker2Symbol[tickerId] : null);
                if (!string.IsNullOrEmpty(symbol) && DictPositions.ContainsKey(symbol))
                {
                    if (tickType == TickType.LastPrice || 
                        (tickType == TickType.ClosePrice && !DictPositions[symbol].MarketPrice.HasValue))
                    {
                        DictPositions[symbol].MarketPrice = price;

                        bool nullFound = false;
                        double totalMktValue = 0;
                        foreach (Asset a in DictPositions.Values)
                        {
                            if (!a.MarketValue.HasValue)
                            {
                                nullFound = true;
                                break;
                            }
                            else
                            {
                                totalMktValue += a.MarketValue.Value;
                            }
                        }
                        if (!nullFound)
                        //if (DictPositions.Values.Count(a => a.MarketValue.HasValue) == DictPositions.Values.Count)
                        {
                            foreach (Asset a in DictPositions.Values)
                                a.Percentage = a.MarketValue.Value / totalMktValue;

                            // Copy DictPositons to Portfolio model object
                            mainThreadDispatcher.Invoke(() =>
                            {
                                Portfolio.Instance.Populate(DictPositions.Values);
                            });
                        }
                    }
                }
            }

            Console.WriteLine(string.Format("tickPrice: price={0}", price));
        }

        /**
         * @brief Market data tick size callback.
         * Handles all size-related ticks.
         * @param tickerId the request's unique identifier.
         * @param field the type of size being received (i.e. bid size)
         * @param size the actual size.
         * @see reqMarketData()
         * @sa TickType, tickPrice, tickString, tickEFP, tickGeneric, tickOptionComputation, tickSnapshotEnd, marketDataType, EClientSocket::reqMktData
         */
        void EWrapper.tickSize(int tickerId, int field, int size)
        {
            Console.WriteLine(string.Format("tickSize: size={0}", size));
        }

        /**
         * @brief Market data callback.
         * @param tickerId the request's unique identifier.
         * @param field the type of the tick being received
         * @param value
         * @sa TickType, tickSize, tickPrice, tickEFP, tickGeneric, tickOptionComputation, tickSnapshotEnd, marketDataType, EClientSocket::reqMktData
         */
        void EWrapper.tickString(int tickerId, int field, string value)
        {
            Console.WriteLine(string.Format("tickString: value={0}", value));
        }

        /**
         * @brief Market data callback.
         * @param tickerId the request's unique identifier.
         * @param field the type of tick being received.
         * @param value
         */
        void EWrapper.tickGeneric(int tickerId, int field, double value)
        {
            Console.WriteLine(string.Format("tickGeneric: value={0}", value));
        }

        /**
         * @brief Exchange for Physicals.
         * @param tickerId The request's identifier.
         * @param tickType The type of tick being received.
         * @param basisPoints Annualized basis points, which is representative of the financing rate that can be directly compared to broker rates.
         * @param formattedBasisPoints Annualized basis points as a formatted string that depicts them in percentage form.
         * @param impliedFuture The implied Futures price.
         * @param holdDays The number of hold days until the expiry of the EFP.
         * @param futureExpiry The expiration date of the single stock future.
         * @param dividendImpact The dividend impact upon the annualized basis points interest rate.
         * @param dividendsToExpiry The dividends expected until the expiration of the single stock future.
         */
        void EWrapper.tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureExpiry, double dividendImpact, double dividendsToExpiry)
        { }

        /**
         * @brief -
         * Upon accepting a Delta-Neutral DN RFQ(request for quote), the server sends a deltaNeutralValidation() message with the 
         * UnderComp structure. If the delta and price fields are empty in the original request, the confirmation will contain the current
         * values from the server. These values are locked when RFQ is processed and remain locked unitl the RFQ is cancelled.
         * @param reqId the request's identifier.
         * @param underComp Underlying Component
         */
        void EWrapper.deltaNeutralValidation(int reqId, IBApi.UnderComp underComp)
        { }

        /**
         * @brief Receive's option specific market data.
         * This method is called when the market in an option or its underlier moves. TWS’s option model volatilities, prices, and deltas, along with the present value of dividends expected on that options underlier are received.
         * @sa TickType, tickSize, tickPrice, tickEFP, tickGeneric, tickString, tickSnapshotEnd, marketDataType, EClientSocket::reqMktData
         */
        void EWrapper.tickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
        { }

        /**
         * @brief When requesting market data snapshots, this market will indicate the snapshot reception is finished.
         * 
         */
        void EWrapper.tickSnapshotEnd(int tickerId)
        {
            Console.WriteLine(string.Format("tickSnapshotEnd: tickerId={0}", tickerId));
        }

        /**
         * @brief Receives next valid order id.
         * @param orderId the next order id
         * @sa EClientSocket::reqIds
         */
        void EWrapper.nextValidId(int orderId)
        {
            Console.WriteLine(string.Format("nextValidId: orderId={0}", orderId));
            nextOrderID = orderId;
        }

        /**
         * @brief Receives a comma-separated string with the managed account ids
         * @sa EClientSocket::reqManagedAccts
         */
        void EWrapper.managedAccounts(string accountsList)
        {
            Console.WriteLine(string.Format("managedAccounts: accountsList={0}", accountsList));
        }

        /**
         * @brief Notifes when the API-TWS connectivity has been closed.
         * @sa EClientSocket::eDisconnect
         */
        void EWrapper.connectionClosed()
        { }

        /**
         * @brief Receives the account information.
         * This method will receive the account information just as it appears in the TWS' Account Summary Window.
         * @param reqId the request's unique identifier.
         * @param account the account id
         * @param tag the account's attribute being received.
         * @param value the account's attribute's value.
         * @param currency the currency on which the value is expressed.
         * @sa accountSummaryEnd, EClientSocket::reqAccountSummary
         */
        void EWrapper.accountSummary(int reqId, string account, string tag, string value, string currency)
        { }

        /**
         * @brief notifies when all the accounts' information has ben received.
         * @param reqId the request's identifier.
         * @sa accountSummary, EClientSocket::reqAccountSummary
         */
        void EWrapper.accountSummaryEnd(int reqId)
        { }

        /*
         * @brief Delivers the Bond contract data after this has been requested via reqContractDetails
         * @param reqId the request's identifier
         * @param contract the bond contract's information.
         * @sa reqContractDetails
         */
        void EWrapper.bondContractDetails(int reqId, IBApi.ContractDetails contract)
        { }

        /**
         * @brief Receives the subscribed account's information.
         * Only one account can be subscribed at a time.
         * @param key the value being updated.
         * @param value up-to-date value
         * @param currency the currency on which the value is expressed.
         * @param accountName the account
         * @sa updatePortfolio, updateAccountTime, accountDownloadEnd, EClientSocket::reqAccountUpdates
         */
        void EWrapper.updateAccountValue(string key, string value, string currency, string accountName)
        { }

        /**
         * @brief Receives the subscribed account's portfolio.
         * This function will receive only the portfolio of the subscribed account. If the portfolios of all managed accounts are needed, refer to EClientSocket::reqPosition
         * @param contract the Contract for which a position is held.
         * @param position the number of positions held.
         * @param marketPrice instrument's unitary price
         * @param marketValue total market value of the instrument.
         * @sa updateAccountTime, accountDownloadEnd, updateAccountValue, EClientSocket::reqAccountUpdates
         */
        void EWrapper.updatePortfolio(IBApi.Contract contract, int position, double marketPrice, double marketValue,
            double averageCost, double unrealisedPNL, double realisedPNL, string accountName)
        { }

        /**
         * @brief Receives the last time on which the account was updated.
         * @param timestamp the last update system time.
         * @sa updatePortfolio, accountDownloadEnd, updateAccountValue, EClientSocket::reqAccountUpdates
         */
        void EWrapper.updateAccountTime(string timestamp)
        { }

        /**
         * @brief Notifies when all the account's information has finished.
         * @param account the account's id
         * @sa updateAccountTime, updatePortfolio, updateAccountValue, EClientSocket::reqAccountUpdates
         */
        void EWrapper.accountDownloadEnd(string account)
        { }

        /**
         * @brief Gives the up-to-date information of an order every time it changes.
         * @param orderId the order's client id.
         * @param status the current status of the order:
         *      PendingSubmit - indicates that you have transmitted the order, but have not yet received confirmation that it has been accepted by the order destination. NOTE: This order status is not sent by TWS and should be explicitly set by the API developer when an order is submitted.
         *      PendingCancel - indicates that you have sent a request to cancel the order but have not yet received cancel confirmation from the order destination. At this point, your order is not confirmed canceled. You may still receive an execution while your cancellation request is pending. NOTE: This order status is not sent by TWS and should be explicitly set by the API developer when an order is canceled.
         *      PreSubmitted - indicates that a simulated order type has been accepted by the IB system and that this order has yet to be elected. The order is held in the IB system until the election criteria are met. At that time the order is transmitted to the order destination as specified .
         *      Submitted - indicates that your order has been accepted at the order destination and is working.
         *      ApiCanceled - after an order has been submitted and before it has been acknowledged, an API client client can request its cancelation, producing this state.
         *      Cancelled - indicates that the balance of your order has been confirmed canceled by the IB system. This could occur unexpectedly when IB or the destination has rejected your order.
         *      Filled - indicates that the order has been completely filled.
         *      Inactive - indicates that the order has been accepted by the system (simulated orders) or an exchange (native orders) but that currently the order is inactive due to system, exchange or other issues.
         * @param filled number of filled positions.
         * @param remaining the remnant positions.
         * @param avgFillPrice average filling price.
         * @param permId the order's permId used by the TWs to identify orders.
         * @param parentId parent's id. Used for bracker and auto trailing stop orders.
         * @param lastFillPrice price at which the last positions were filled.
         * @param clientId API client which submitted the order.
         * @param whyHeld this field is used to identify an order held when TWS is trying to locate shares for a short sell. The value used to indicate this is 'locate'.
         * @sa openOrder, openOrderEnd, EClientSocket::placeOrder, EClientSocket::reqAllOpenOrders, EClientSocket::reqAutoOpenOrders
         */
        void EWrapper.orderStatus(int orderId, string status, int filled, int remaining, double avgFillPrice,
            int permId, int parentId, double lastFillPrice, int clientId, string whyHeld)
        { }

        /**
         * @brief Feeds in currently open orders.
         * @param orderId the order's unique id
         * @param contract the order's Contract.
         * @param order the currently active Order.
         * @param orderState the order's OrderState
         * @sa orderStatus, openOrderEnd, EClientSocket::placeOrder, EClientSocket::reqAllOpenOrders, EClientSocket::reqAutoOpenOrders
         */
        void EWrapper.openOrder(int orderId, IBApi.Contract contract, IBApi.Order order, IBApi.OrderState orderState)
        { }

        /**
         * @brief Notifies the end of the open orders' reception.
         * @sa orderStatus, openOrder, EClientSocket::placeOrder, EClientSocket::reqAllOpenOrders, EClientSocket::reqAutoOpenOrders
         */
        void EWrapper.openOrderEnd()
        { }

        /**
         * @brief receives the full contract's definitons
         * This method will return all contracts matching the requested via EClientSocket::reqContractDetails. For example, one can obtain the whole option chain with it.
         * @param reqId the unique request identifier
         * @param contractDetails the instrument's complete definition.        
         * @sa contractDetailsEnd, EClientSocket::reqContractDetails
         */
        void EWrapper.contractDetails(int reqId, IBApi.ContractDetails contractDetails)
        { }

        /**
         * @brief After all contracts matching the request were returned, this method will mark the end of their reception. 
         * @param reqId the request's identifier
         * @sa contractDetails, EClientSocket::reqContractDetails
         */
        void EWrapper.contractDetailsEnd(int reqId)
        { }

        /**
         * @brief Provides the executions which happened in the last 24 hours.
         * @param reqId the request's identifier
         * @param contract the Contract of the Order
         * @param execution the Execution details.
         * @sa execDetailsEnd, commissionReport, EClientSocket::reqExecutions, Execution
         */
        void EWrapper.execDetails(int reqId, IBApi.Contract contract, IBApi.Execution execution)
        { }

        /**
         * @brief indicates the end of the Execution reception.
         * @param reqId the request's identifier
         * @sa execDetails, commissionReport, EClientSocket::reqExecutions
         */
        void EWrapper.execDetailsEnd(int reqId)
        { }

        /**
         * @brief provides the CommissionReport of an Execution
         * @sa execDetails, execDetailsEnd, EClientSocket::reqExecutions, CommissionReport
         */
        void EWrapper.commissionReport(IBApi.CommissionReport commissionReport)
        { }

        /**
         * @brief returns Reuters' Fundamental data
         * @param reqId the request's identifier
         * @param data Reuthers xml-formatted fundamental data
         * @sa EClientSocket::reqFundamentalData
         */
        void EWrapper.fundamentalData(int reqId, string data)
        { }

        /**
         * @brief returns the requested historical data bars
         * @param reqId the request's identifier
         * @param date the bar's date and time (either as a yyyymmss hh:mm:ss formatted string or as system time according to the request)
         * @param open the bar's open point
         * @param high the bar's high point
         * @param low the bar's low point
         * @param close the bar's closing point
         * @param volume the bar's traded volume if available
         * @param count the number of trades during the bar's timespan (only available for TRADES).
         * @param WAP the bar's Weighted Average Price
         * @param hasGaps indicates if the data has gaps or not.
         * @sa EClientSocket::reqHistoricalData
         */
        void EWrapper.historicalData(int reqId, string date, double open, double high, double low, double close, int volume, int count, double WAP, bool hasGaps)
        { }

        /**
         * @brief Marks the ending of the historical bars reception.
         * 
         */
        void EWrapper.historicalDataEnd(int reqId, string start, string end)
        { }

        /**
         * @brief Returns the current market data type (frozen or real time streamed)
         * @param reqId the request's identifier
         * @param marketDataType 1 for real time, 2 for frozen
         * @sa EClientSocket::reqMarketDataType
         */
        void EWrapper.marketDataType(int reqId, int marketDataType)
        { }

        /**
         * @brief Returns the order book
         * @param tickerId the request's identifier
         * @param position the order book's row being updated
         * @param operation how to refresh the row:
         *      0 = insert (insert this new order into the row identified by 'position')·
         *      1 = update (update the existing order in the row identified by 'position')·
         *      2 = delete (delete the existing order at the row identified by 'position').
         * @param side 0 for ask, 1 for bid
         * @param price the order's price
         * @param size the order's size
         * @sa updateMktDepthL2, EClientSocket::reqMarketDepth
         */
        void EWrapper.updateMktDepth(int tickerId, int position, int operation, int side, double price, int size)
        { }

        /**
         * @brief Returns the order book
         * @param tickerId the request's identifier
         * @param position the order book's row being updated
         * @param marketMaker the exchange holding the order
         * @param operation how to refresh the row:
         *      0 - insert (insert this new order into the row identified by 'position')·
         *      1 - update (update the existing order in the row identified by 'position')·
         *      2 - delete (delete the existing order at the row identified by 'position').
         * @param side 0 for ask, 1 for bid
         * @param price the order's price
         * @param size the order's size
         * @sa updateMktDepth, EClientSocket::reqMarketDepth
         */
        void EWrapper.updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size)
        { }

        /**
         * @brief provides IB's bulletins
         * @param msgId the bulletin's identifier
         * @param msgType one of:
         *      1 - Regular news bulletin
         *      2 - Exchange no longer available for trading
         *      3 - Exchange is available for trading
         * @param message the message
         * @param origExchange the exchange where the message comes from.
         */
        void EWrapper.updateNewsBulletin(int msgId, int msgType, String message, String origExchange)
        { }

        /**
         * @brief provides the portfolio's open positions.
         * @param account the account holding the position.
         * @param contract the position's Contract
         * @param pos the number of positions held.
         * @Param avgCost the average cost of the position.
         * @sa positionEnd, EClientSocket::reqPositions
         */
        void EWrapper.position(string account, IBApi.Contract contract, int pos, double avgCost)
        {
            if (!dictTicker2Symbol.ContainsValue(contract.Symbol))
            {
                // Keep track which ticketID maps to which Symbol
                dictTicker2Symbol.Add(++tickerID, contract.Symbol);

                // 211 - Mark Price
                ClientSocket.reqMktData(tickerID, contract, "211", true, null);
            }

            if (DictPositions.ContainsKey(contract.Symbol))
            {
                // Update applicable properties
                DictPositions[contract.Symbol].Position = pos;
                DictPositions[contract.Symbol].AvgCost = avgCost;
            }
            else
            {
                DictPositions.Add(contract.Symbol, new Asset
                {
                    Symbol = contract.Symbol,
                    Name = contract.LocalSymbol,
                    Market = contract.Exchange,
                    Position = pos,
                    AvgCost = avgCost,
                    Currency = contract.Currency,
                    FxRate = CcyExchangeRate.Table[contract.Currency]
                });
            }
        }

        /**
         * @brief Indicates all the positions have been transmitted.
         * @sa position, reqPositions
         */
        void EWrapper.positionEnd()
        {
        }

        /**
         * @brief updates the real time 5 seconds bars
         * @param reqId the request's identifier
         * @param date the bar's date and time (either as a yyyymmss hh:mm:ss formatted string or as system time according to the request)
         * @param open the bar's open point
         * @param high the bar's high point
         * @param low the bar's low point
         * @param close the bar's closing point
         * @param volume the bar's traded volume if available
         * @param WAP the bar's Weighted Average Price
         * @param count the number of trades during the bar's timespan (only available for TRADES).
         * @sa EClientSocket::reqRealTimeBars
         */
        void EWrapper.realtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double WAP, int count)
        { }

        /**
         * @brief provides the xml-formatted parameters available to create a market scanner.
         * @param xml the xml-formatted string with the available parameters.
         * @sa scannerData, EClientSocket::reqScannerParameters
         */
        void EWrapper.scannerParameters(string xml)
        { }

        /**
         * @brief provides the data resulting from the market scanner request.
         * @param reqid the request's identifier.
         * @param rank the ranking within the response of this bar.
         * @param contractDetails the data's ContractDetails
         * @param distance according to query.
         * @param benchmark according to query.
         * @param projection according to query.
         * @param legStr describes the combo legs when the scanner is returning EFP
         * @sa scannerParameters, scannerDataEnd, EClientSocket::reqScannerSubscription
         */
        void EWrapper.scannerData(int reqId, int rank, IBApi.ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr)
        { }

        /**
         * @brief Indicates the scanner data reception has terminated.
         * @param reqId the request's identifier
         * @sa scannerParameters, scannerData, EClientSocket>>reqScannerSubscription
         */
        void EWrapper.scannerDataEnd(int reqId)
        { }

        /**
         * @brief receives the Financial Advisor's configuration available in the TWS
         * @param faDataType one of:
         *    1. Groups: offer traders a way to create a group of accounts and apply a single allocation method to all accounts in the group.
         *    2. Profiles: let you allocate shares on an account-by-account basis using a predefined calculation value.
         *    3. Account Aliases: let you easily identify the accounts by meaningful names rather than account numbers.
         * @param faXmlData the xml-formatted configuration
         * @sa EClientSocket::requestFA, EClientSocket::replaceFA
         */
        void EWrapper.receiveFA(int faDataType, string faXmlData)
        { }

        void EWrapper.verifyMessageAPI(string apiData)
        { }
        void EWrapper.verifyCompleted(bool isSuccessful, string errorText)
        { }
        void EWrapper.displayGroupList(int reqId, string groups)
        { }
        void EWrapper.displayGroupUpdated(int reqId, string contractInfo)
        { }

    }
}
