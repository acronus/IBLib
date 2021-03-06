using IBApi;
using IBSampleApp.messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Diagnostics;
using static IBLib.Api;

namespace IBLib
{
    [Guid("EAA4976A-45C3-4BC5-BC0B-E474F4C3C23F"),
        ComVisible(true)
    ]
    public interface IApi
    {
        void ConnectApi(string host = "", int port = 7497, int clientID = 0);
        bool IsConnected();
        void DisconnectApi();
        void SetMarketDataType(int mtype);
        void AskForPrice(int symbolID, string genericTickList = "233");
        void StopAskingPrice();
        void AskForRTBar(int symbolID, string WhatToShow);
        void AskForHistBar(int symbolID, string WhatToShow, bool keepUpToDate);
        void AskForSomeHistBar(int symbolID, string WhatToShow);
        void AskForPositions();
        void AskForOpenOrders();
        void SaveHistBars(int symbolID, string WhatToShow, string durationString, string saveFileName, int decimals);
        void StopRTBar();
        string GetBarsAsText(int symbolID);
        void GetBarsAsRecord(int symbolID, out RTbars rtbars);
        void GetBarsAsArrays(int symbolID, out string[] date, out string[] time, out double[] open, out double[] high,
            out double[] low, out double[] close);
        int GetNewOrderID();
        void PlaceOrder(int orderID, Contract contract, Order order);
        void CancelOrder(int id);
        void CancelAllOrders();
        void CancelOrdersBySymbol(string symbol);
        void GetBuySellCount(string symbol, out int buy, out int sell);
        Order GetOrder(string Action, string OrderType, double TotalQuantity, double LmtPrice);
        Contract GetContractByConID(int conID);
        Contract GetContractBySymbolID(int symbolID);
        Contract GetContract(string Symbol, string SecType, string Exchange, string Currency,
            string LocalSymbol, bool IncludeExpired, string LastTradeDateOrContractMonth, string PrimaryExch,
            string PUTorCALL, double Strike, string Multiplier);
        Symbol GetAll(int id);
        void GetContractDetails(int symbolID);
        void AddSymbol(int id, string name, Contract contract);
        string GetErrors();
        void GetHistBarsAsArrays(int symbolID, out string[] date, out string[] time, out double[] open, out double[] high,
            out double[] low, out double[] close);
        void GetRealBarsAsArrays(int symbolID, out string[] date, out string[] time, out double[] open, out double[] high,
            out double[] low, out double[] close);
        void GetLast200Bars(int symbolID, out string[] date, out string[] time, out double[] open, out double[] high,
            out double[] low, out double[] close);
    }

    [Guid("EAA4976A-45C3-4BC5-BC0B-E474F4C3C13F"),
        ClassInterface(ClassInterfaceType.None),
        ComVisible(true)
    ]
    public class Api : IApi
    {
        static SynchronizationContext sc = new SynchronizationContext();
        public Samples.EWrapperImpl ibClient = new Samples.EWrapperImpl(sc);

        public int clientID;
        public int baseID = 1000000;
        public int ReqId = 1;

        public struct lBar
        {
            public long date;
            public double open;
            public double high;
            public double low;
            public double close;
        }

        public struct mBar
        {
            public string date;
            public string time;
            public double open;
            public double high;
            public double low;
            public double close;
        }

        public struct RTbars
        {
            public mBar last5s;
            public mBar min1;
            public mBar min2;
            public mBar min3;
            public mBar min4;
            public mBar min5;
            public mBar min6;
            public mBar min7;
            public mBar min8;
            public mBar min9;
        }

        public struct Symbol
        {
            public int id;
            public string name;
            public int ConId;

            public Contract contract;
            public ContractDetails contractDetails;
            public double lastprice;
            public double position;
            public double buyVolume;
            public double sellVolume;

            public int contrReqId;
            public int priceReqId;
            public int RTbarReqId;
            public int histBarReqId;
            public int someHistBarReqId;
            public int execId;

            public int saveHistBarReqId;
            public string saveFileName;
            public int saveDecimals;
        }

        public struct SymbolUtility
        {
            public int id;

            public Contract contract;
            public ContractDetails contractDetails;

            public bool startMinBar;
            public bool endMinBar;
            public mBar[] bars;
            public mBar currentBar;
            public mBar[] minbars;
        }

        public struct OpenOrder
        {
            public int permId;
            public double totalquantity;
            public Contract contract;
            public Order order;
            public OrderState state;
        }

        public struct Trade
        {
            public int permId;
            public string symbol;
            public double filled;
            public Contract contract;
            public Execution execution;
        }

        Symbol[] all = new Symbol[30];
        SymbolUtility[] uall = new SymbolUtility[30];

        StringBuilder errors = new StringBuilder();

        Dictionary<long, List<lBar>> histBars = new Dictionary<long, List<lBar>>();
        Dictionary<long, List<lBar>> realBars = new Dictionary<long, List<lBar>>();

        Dictionary<int, OpenOrder> openOrders = new Dictionary<int, OpenOrder>();
        Dictionary<string, Trade> trades = new Dictionary<string, Trade>();

        //bool connected = true;
        //Stopwatch stopwatch;

        public Api()
        {
            ibClient.TickPrice += IbClient_TickPrice;
            ibClient.RealtimeBar += IbClient_RealtimeBar;
            ibClient.OpenOrder += IbClient_OpenOrder;
            ibClient.OpenOrderEnd += IbClient_OpenOrderEnd;
            ibClient.OrderStatus += IbClient_OrderStatus;
            ibClient.Position += IbClient_Position;
            ibClient.PositionEnd += IbClient_PositionEnd;
            ibClient.ExecDetails += IbClient_ExecDetails;
            ibClient.ContractDetails += IbClient_ContractDetails;
            ibClient.Error += IbClient_Error;
            ibClient.HistoricalData += IbClient_HistoricalData;
            ibClient.HistoricalDataUpdate += IbClient_HistoricalDataUpdate;
            ibClient.HistoricalDataEnd += IbClient_HistoricalDataEnd;

            /*
            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEventA);
            aTimer.Interval = 5000;
            aTimer.Enabled = true;
            */
        }

        private void IbClient_ExecDetails(ExecutionMessage exec)
        {
            Contract con = exec.Contract;
            Execution ex = exec.Execution;
            string exid = ex.ExecId;

            // only live orders
            if (openOrders.ContainsKey(ex.PermId))
            {
                if (!trades.ContainsKey(exid))
                {
                    Trade t = new Trade();
                    t.permId = ex.PermId;
                    t.symbol = con.Symbol;
                    t.filled = ex.Shares;
                    t.contract = con;
                    t.execution = ex;

                    trades.Add(exid, t);

                    //recalc volumes
                    CalcOpenOrdersVolume(con.Symbol);
                }
            }
        }

        private void IbClient_OpenOrderEnd()
        {
            ibClient.ClientSocket.reqExecutions(getReqId(), new ExecutionFilter());
        }

        private void IbClient_OpenOrder(OpenOrderMessage orderMessage)
        {
            OpenOrder opo = new OpenOrder();
            opo.contract = orderMessage.Contract;
            opo.order = orderMessage.Order;
            opo.state = orderMessage.OrderState;
            opo.permId = orderMessage.Order.PermId;
            opo.totalquantity = orderMessage.Order.TotalQuantity;

            if (!openOrders.ContainsKey(orderMessage.Order.PermId) && (orderMessage.OrderState.Status == "Submitted"))
            {
                lock (openOrders)
                {
                    openOrders.Add(orderMessage.Order.PermId, opo);
                }
            }/*
            else
            {
                    openOrders[openOrder.Order.PermId] = opo;
            }*/

            //Remove cancelled or completely filled order
            if (openOrders.ContainsKey(orderMessage.Order.PermId) &&
                ((orderMessage.OrderState.Status == "Filled") || (orderMessage.OrderState.Status == "Cancelled")))
            {
                lock (openOrders)
                {
                    openOrders.Remove(orderMessage.Order.PermId);
                }
            }

            // recalc volumes
            CalcOpenOrdersVolume(opo.contract.Symbol);
        }

        private void IbClient_OrderStatus(OrderStatusMessage status)
        {
            // Remove cancelled
            if (status.Status == "Cancelled")
            {
                if (openOrders.ContainsKey(status.PermId))
                {
                    OpenOrder opo = openOrders[status.PermId];
                    lock (openOrders)
                    {   
                        openOrders.Remove(status.PermId);
                    }
                    CalcOpenOrdersVolume(opo.contract.Symbol);
                }
            }
        }

        private void CalcOpenOrdersVolume(string symbol)
        {
            double buyvolume = 0;
            double sellvolume = 0;

            foreach (OpenOrder o in openOrders.Values.Where(o => o.contract.Symbol == symbol))
            {
                if (o.order.Action == "BUY")
                {
                    buyvolume += o.totalquantity - CalcTradesByPermId(o.permId);
                }
                else
                {
                    sellvolume += o.totalquantity - CalcTradesByPermId(o.permId);
                }
            }

            foreach (Symbol s in all.Where(s => s.id > 0).Where(a => a.contract.Symbol == symbol))
            {
                all[s.id].buyVolume = buyvolume;
                all[s.id].sellVolume = sellvolume;
            }
        }

        private double CalcTradesByPermId(int permID)
        {
            double filled = 0;

            foreach (Trade tr in trades.Values.Where(t => t.permId == permID))
            {
                filled += tr.filled;
            }

            return filled;
        }

        private void OnTimedEventA(object source, ElapsedEventArgs e)
        {
            /*
            //MessageBox.Show("123");
            if (!IsConnected()) // connection lost
            {
                connected = false;

                DateTime localDate = DateTime.Now;

                errors.Append("IBLib: Connection lost at " + localDate.ToString() + Environment.NewLine);
                //stopwatch = Stopwatch.StartNew();
            }

            if (IsConnected() && (connected == false)) // connection reestablished
            {
                connected = true;
                DateTime localDate = DateTime.Now;
                errors.Append("IBLib: Connection reestablished at " + localDate.ToString() + Environment.NewLine);

                foreach (Symbol s in all.Where(s => s.id>0))
                {
                    //
                }
            }*/
        }

        private void IbClient_HistoricalDataUpdate(HistoricalDataMessage bar)
        {
            foreach (Symbol s in all.Where(s => s.id > 0))
            {
                if (s.histBarReqId == bar.RequestId)
                {
                    int symbolID = s.id;

                    lBar r = new lBar();
                    r.date = Int64.Parse(bar.Date);
                    r.open = bar.Open;
                    r.high = bar.High;
                    r.low = bar.Low;
                    r.close = bar.Close;

                    lock (realBars[symbolID])
                    {
                        if (r.open > 0)
                            realBars[symbolID].Add(r);
                    }

                }
            }

        }

        private void IbClient_HistoricalDataEnd(HistoricalDataEndMessage obj)
        {/*
            foreach (Symbol s in all)
            {
                if (s.saveHistBarReqId == obj.RequestId)
                {
                    int symbolID = s.id;

                    if (all[symbolID].saveFileName != "") {
                        StringBuilder sb = new StringBuilder();

                        foreach (lBar bar in saveBars[symbolID])
                        {
                            DateTime t = UnixTimestampToDateTime(bar.date);
                            string date = t.ToString("yyyyMMdd");
                            string time = t.ToString("HHmmss");
                            double open = bar.open;
                            double high = bar.high;
                            double low = bar.low;
                            double close = bar.close;

                            sb.AppendLine(date + ";" + time + ";" + open + ";" + high + ";" + low + ";" + close);
                        }

                        
                        File.WriteAllText(all[symbolID].saveFileName, sb.ToString());
                        all[symbolID].saveFileName = "";
                    }
                }
            }*/
        }

        static DateTime UnixTimestampToDateTime(long unixTimestamp)
        {
            DateTime unixBaseTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return unixBaseTime.AddSeconds(unixTimestamp);
        }

        private void IbClient_HistoricalData(HistoricalDataMessage bar)
        {
            foreach (Symbol s in all.Where(s => s.id > 0))
            {

                int symbolID = s.id;

                lBar l = new lBar();
                l.date = Int64.Parse(bar.Date);
                l.open = bar.Open;
                l.high = bar.High;
                l.low = bar.Low;
                l.close = bar.Close;

                if (s.histBarReqId == bar.RequestId)
                {
                    histBars[symbolID].Add(l);
                }

                if (s.someHistBarReqId == bar.RequestId)
                {
                    lock (realBars[symbolID])
                    {
                        if (l.open > 0)
                        {
                            if (!realBars[symbolID].Contains(l))
                            {
                                realBars[symbolID].Add(l);
                            }
                        }
                    }
                }

                if (s.saveHistBarReqId == bar.RequestId)
                {
                    //saveBars[symbolID].Add(l);

                    DateTime t = UnixTimestampToDateTime(l.date);
                    string date = t.ToString("yyyyMMdd");
                    string time = t.ToString("HHmmss");
                    double open = l.open;
                    double high = l.high;
                    double low = l.low;
                    double close = l.close;

                    string sopen = string.Format("{0:f" + all[symbolID].saveDecimals + "}", open);
                    string shigh = string.Format("{0:f" + all[symbolID].saveDecimals + "}", high);
                    string slow = string.Format("{0:f" + all[symbolID].saveDecimals + "}", low);
                    string sclose = string.Format("{0:f" + all[symbolID].saveDecimals + "}", close);

                    string str = s.name + ";1;" + date + ";" + time + ";" + sopen + ";" + shigh + ";" + slow + ";" + sclose + ";" + Environment.NewLine;
                    File.AppendAllText(all[symbolID].saveFileName, str);
                }
            }
        }

        private void IbClient_Error(int RequestId, int ErrorCode, string Message, Exception ex)
        {
            try
            {
                errors.Append("Error.Request: " + RequestId + ", Code: " + ErrorCode + " - " + Message + Environment.NewLine);
            }
            catch (Exception) { }
        }

        private void IbClient_ContractDetails(ContractDetailsMessage contract)
        {
            foreach (Symbol s in all.Where(s => s.id > 0))
            {
                if (s.contrReqId == contract.RequestId)
                {
                    all[s.id].contractDetails = contract.ContractDetails;
                    all[s.id].ConId = contract.ContractDetails.Contract.ConId;
                }
            }
            //contracts.Add(contract.ContractDetails.Contract.ConId, contract.ContractDetails.Contract.Symbol);
        }

        private void IbClient_PositionEnd()
        {
            //tcs.SetResult(true);
        }

        private void IbClient_Position(PositionMessage Position)
        {
            foreach (Symbol s in all.Where(s => s.id > 0))
            {
                //MessageBox.Show(s.name);
                Contract c = s.contract;
                if ((c != null) && (c.Symbol == Position.Contract.Symbol))
                {
                    all[s.id].position = Position.Position;
                }
            }
        }


        private void IbClient_RealtimeBar(RealTimeBarMessage bar)
        {
            foreach (Symbol s in all.Where(s => s.id > 0))
            {
                if (s.RTbarReqId == bar.RequestId)
                {
                    int symbolID = s.id;

                    lBar r = new lBar();
                    r.date = Int64.Parse(bar.Date);
                    r.open = bar.Open;
                    r.high = bar.High;
                    r.low = bar.Low;
                    r.close = bar.Close;

                    lock (realBars[symbolID])
                    {
                        if (r.open > 0)
                            realBars[symbolID].Add(r);
                    }

                    /*
                    if (realBars[symbolID].Count() > 200)
                    {
                        histBars[symbolID].Clear();
                        realBars[symbolID].RemoveAt(0);
                    }*/

                }
            }
        }

        private void IbClient_TickPrice(TickPriceMessage dataMessage)
        {
            if ((dataMessage.Field == TickType.LAST) || (dataMessage.Field == TickType.DELAYED_LAST))
            {
                foreach (Symbol s in all.Where(s => s.id > 0))
                {
                    if (s.priceReqId == dataMessage.RequestId)
                    {
                        all[s.id].lastprice = dataMessage.Price;
                        // MessageBox.Show(dataMessage.Price.ToString());
                    }
                }
            }
        }

        public void ConnectApi(string host = "", int port = 7497, int clientID = 0)
        {
            if (!IsConnected())
            {
                // Connect to the IB Server through TWS. Parameters are:
                // host       - Host name or IP address of the host running TWS
                // port       - The port TWS listens through for connections
                // clientId   - The identifier of the client application
                ibClient.ClientSocket.eConnect(host, port, clientID);

                var reader = new EReader(ibClient.ClientSocket, ibClient.Signal);
                reader.Start();
                new Thread(() =>
                {
                    while (ibClient.ClientSocket.IsConnected())
                    {
                        ibClient.Signal.waitForSignal();
                        reader.processMsgs();
                    }
                })
                {
                    IsBackground = true
                }.Start();

                // Pause here until the connection is complete
                while (ibClient.NextOrderId <= 0) { }

                this.clientID = clientID;
            }
        }

        public bool IsConnected()
        {
            return ibClient.ClientSocket.IsConnected();
        }
        public void DisconnectApi()
        {
            if (IsConnected())
            {
                ibClient.ClientSocket.eDisconnect();
                all = new Symbol[30];
                uall = new SymbolUtility[30];
                histBars = new Dictionary<long, List<lBar>>();
                realBars = new Dictionary<long, List<lBar>>();
                openOrders = new Dictionary<int, OpenOrder>();
            }
        }

        //Live	1; Frozen	2; Delayed	3;Delayed Frozen	4;
        public void SetMarketDataType(int mtype)
        {
            ibClient.ClientSocket.reqMarketDataType(mtype);
        }

        public void AskForPrice(int symbolID, string genericTickList = "233")
        {
            List<TagValue> mktDataOptions = new List<TagValue>();

            int req = getReqId();

            all[symbolID].priceReqId = req;
            //uall[symbolID].priceReqId = req;

            ibClient.ClientSocket.reqMktData(all[symbolID].priceReqId, all[symbolID].contract,
                genericTickList, false, false, mktDataOptions);
        }

        public void StopAskingPrice()
        {
            //ibClient.ClientSocket.cancelMktData(getReqId());
        }

        public void AskForRTBar(int symbolID, string WhatToShow)
        {
            List<TagValue> DataOptions = new List<TagValue>();

            //uall[symbolID].realBarsIndex = 0;

            int req = getReqId();

            all[symbolID].RTbarReqId = req;
            //uall[symbolID].RTbarReqId = req;

            //WhatToShow BID_ASK MIDPOINT TRADES etc
            ibClient.ClientSocket.reqRealTimeBars(all[symbolID].RTbarReqId, all[symbolID].contract, 5, WhatToShow, true, DataOptions);
        }

        public void AskForHistBar(int symbolID, string WhatToShow, bool keepUpToDate = false)
        {
            string endDateTime = "";
            string durationString = "1000 S";
            string barSizeSetting = "5 secs";

            int useRTH = 0;

            //uall[symbolID].histBarsIndex = 0;

            int req = getReqId();
            all[symbolID].histBarReqId = req;
            //uall[symbolID].histBarReqId = req;

            //uall[symbolID].histBars = new mBar[300];

            histBars[symbolID].Clear();

            //ibClient.ClientSocket.reqHistoricalData( 1000, all[symbolID].contract, "", "1000 S", "5 secs", "MIDPOINT", 0, 1, false, new List<TagValue>());

            ibClient.ClientSocket.reqHistoricalData(all[symbolID].histBarReqId, all[symbolID].contract, endDateTime, durationString, barSizeSetting, WhatToShow, useRTH, 2, keepUpToDate, new List<TagValue>());
        }

        public void AskForSomeHistBar(int symbolID, string WhatToShow)
        {
            string endDateTime = "";
            string barSizeSetting = "5 secs";
            string durationString = "30 S";
            bool keepUpToDate = false;

            int useRTH = 0;

            int req = getReqId();
            all[symbolID].someHistBarReqId = req;
            //uall[symbolID].someHistBarReqId = req;

            //histBars[symbolID].Clear();

            ibClient.ClientSocket.reqHistoricalData(all[symbolID].someHistBarReqId, all[symbolID].contract, endDateTime, durationString, barSizeSetting, WhatToShow, useRTH, 2, keepUpToDate, new List<TagValue>());
        }

        public void SaveHistBars(int symbolID, string WhatToShow, string durationString, string saveFileName, int decimals)
        {
            if (saveFileName == "") return;

            string endDateTime = "";
            string barSizeSetting = "5 secs";
            int useRTH = 0;
            bool keepUpToDate = false;

            int req = getReqId();
            all[symbolID].saveHistBarReqId = req;
            all[symbolID].saveFileName = saveFileName;
            all[symbolID].saveDecimals = decimals;

            //saveBars[symbolID].Clear();

            //File.Create(saveFileName);

            ibClient.ClientSocket.reqHistoricalData(all[symbolID].saveHistBarReqId, all[symbolID].contract, endDateTime, durationString, barSizeSetting, WhatToShow, useRTH, 2, keepUpToDate, new List<TagValue>());
        }

        public void StopRTBar()
        {
            /* ibClient.ClientSocket.cancelRealTimeBars(getReqId());
             minbars.Clear();
             startMinBar = false;
             endMinBar = false;*/
        }

        public string GetBarsAsText(int symbolID)
        {
            string currentBarText;

            currentBarText = uall[symbolID].currentBar.date + ";" + uall[symbolID].currentBar.time + ";" + uall[symbolID].currentBar.open + ";" +
                uall[symbolID].currentBar.high + ";" + uall[symbolID].currentBar.low + ";" + uall[symbolID].currentBar.close + ";";

            if (currentBarText.Length < 10) return "";

            string minBarsText = "";

            foreach (mBar minbar in uall[symbolID].minbars)
            {
                minBarsText = minbar.date + ";" + minbar.time + ";" + minbar.open + ";" +
                    minbar.high + ";" + minbar.low + ";" + minbar.close + ";" + minBarsText;
            }

            return currentBarText + minBarsText;
        }

        public void GetBarsAsArrays(int symbolID, out string[] date, out string[] time, out double[] open, out double[] high,
            out double[] low, out double[] close)
        {
            date = new string[10];
            time = new string[10];
            open = new double[10];
            high = new double[10];
            low = new double[10];
            close = new double[10];

            date[0] = uall[symbolID].currentBar.date;
            time[0] = uall[symbolID].currentBar.time;
            open[0] = uall[symbolID].currentBar.open;
            high[0] = uall[symbolID].currentBar.high;
            low[0] = uall[symbolID].currentBar.low;
            close[0] = uall[symbolID].currentBar.close;

            int index = uall[symbolID].minbars.Length;
            foreach (mBar minbar in uall[symbolID].minbars)
            {
                date[index] = minbar.date;
                time[index] = minbar.time;
                open[index] = minbar.open;
                high[index] = minbar.high;
                low[index] = minbar.low;
                close[index] = minbar.close;

                index--;
            }
        }

        public void GetBarsAsRecord(int symbolID, out RTbars rtbars)
        {
            RTbars t = new RTbars();

            t.last5s = uall[symbolID].currentBar;

            //List<mBar> tbar = all[symbolID].minbars.ToArray().ToList<mBar>();

            mBar[] tbar = uall[symbolID].minbars;

            tbar.Reverse();

            mBar[] tmin = new mBar[9];

            Array.Copy(tbar.ToArray(), tmin, tbar.Length);

            t.min1 = tmin[0];
            t.min2 = tmin[1];
            t.min3 = tmin[2];
            t.min4 = tmin[3];
            t.min5 = tmin[4];
            t.min6 = tmin[5];
            t.min7 = tmin[6];
            t.min8 = tmin[7];
            t.min9 = tmin[8];

            rtbars = t;
        }

        public int GetNewOrderID()
        {
            if (ibClient.NextOrderId > 0) return ibClient.NextOrderId;
            return 0;
        }

        public void PlaceOrder(int orderID, Contract contract, Order order)
        {
            ibClient.ClientSocket.placeOrder(orderID, contract, order);
            ibClient.NextOrderId++;
        }

        public void CancelOrder(int id)
        {
            ibClient.ClientSocket.cancelOrder(id);
        }

        public void CancelAllOrders()
        {
            ibClient.ClientSocket.reqGlobalCancel();
        }

        public void CancelOrdersBySymbol(string symbol)
        {
            //symbolToCancel = symbol;
            //ibClient.ClientSocket.reqOpenOrders();
            foreach (OpenOrder o in openOrders.Values.Where(o => o.contract.Symbol == symbol))
            {
                ibClient.ClientSocket.cancelOrder(o.order.OrderId);
            }
        }

        public void GetBuySellCount(string symbol, out int buy, out int sell)
        {
            /* symbolToCntOrders = symbol;
             ibClient.ClientSocket.reqOpenOrders();
             while ((cntBUYorders < 0) && (cntSELLorders < 0)) { }
            */
            buy = 0; //cntBUYorders;
            sell = 0; //cntSELLorders;
            //cntBUYorders = -1;
            //cntSELLorders = -1;
        }

        public void AskForPositions()
        {
            ibClient.ClientSocket.reqPositions();
        }

        public void AskForOpenOrders()
        {
            ibClient.ClientSocket.reqOpenOrders();
        }

        public Order GetOrder(string Action, string OrderType, double TotalQuantity, double LmtPrice)
        {
            Order order = new Order();
            order.Action = Action; //BUY or SELL
            order.OrderType = OrderType; // LMT
            order.TotalQuantity = TotalQuantity;
            order.LmtPrice = LmtPrice;
            order.Tif = "GTC";
            order.Transmit = true;

            return order;
        }

        public Contract GetContractBySymbolID(int symbolID)
        {
            Contract contract = all[symbolID].contract;

            return contract;
        }

        public Contract GetContractByConID(int conID)
        {
            Contract contract = new Contract();
            contract.ConId = conID;

            return contract;
        }

        public Contract GetContract(string Symbol, string SecType, string Exchange, string Currency,
            string LocalSymbol, bool IncludeExpired, string LastTradeDateOrContractMonth, string PrimaryExch,
            string PUTorCALL, double Strike, string Multiplier)
        {
            Contract contract = new Contract();
            contract.Symbol = Symbol;
            contract.SecType = SecType;
            contract.Exchange = Exchange;
            contract.Currency = Currency;
            contract.LocalSymbol = LocalSymbol;
            contract.IncludeExpired = IncludeExpired;
            contract.LastTradeDateOrContractMonth = LastTradeDateOrContractMonth;
            contract.PrimaryExch = PrimaryExch;
            contract.Right = PUTorCALL;
            contract.Strike = Strike;
            contract.Multiplier = Multiplier;

            return contract;
        }

        public Symbol GetAll(int id)
        {
            return all[id];
        }

        public void GetContractDetails(int symbolID)
        {
            ibClient.ClientSocket.reqContractDetails(all[symbolID].contrReqId, all[symbolID].contract);
        }

        public int getReqId()
        {
            return int.Parse(DateTime.Now.ToString("HHmmss")) * (clientID + 1) * 10 + ++ReqId;
        }

        public void AddSymbol(int id, string name, Contract contract)
        {
            // Do not add existing symbols
            foreach (var i in all)
            {
                if (i.id == id) return;
            }

            Symbol s = new Symbol();
            s.id = id;
            s.name = name;
            s.contract = contract;

            s.contrReqId = getReqId();
            s.priceReqId = getReqId();
            s.RTbarReqId = getReqId();
            s.histBarReqId = getReqId();
            s.someHistBarReqId = getReqId();

            SymbolUtility u = new SymbolUtility();

            u.id = id;
            u.contract = contract;
            /*
            u.contrReqId = s.contrReqId;
            u.priceReqId = s.priceReqId;
            u.RTbarReqId = s.RTbarReqId;
            u.histBarReqId = s.histBarReqId;
            u.someHistBarReqId = s.someHistBarReqId;
            */

            u.bars = new mBar[12];
            u.minbars = new mBar[9];

            u.startMinBar = false;
            u.endMinBar = false;

            List<lBar> lbars = new List<lBar>();
            histBars.Add(id, lbars);

            List<lBar> rbars = new List<lBar>();
            realBars.Add(id, rbars);

            all[id] = s;
            //uall[id] = u;
        }

        public string GetErrors()
        {
            string t = errors.ToString();
            errors.Clear();
            return t;
        }

        public void GetHistBarsAsArrays(int symbolID, out string[] date, out string[] time, out double[] open, out double[] high,
            out double[] low, out double[] close)
        {
            date = new string[300];
            time = new string[300];
            open = new double[300];
            high = new double[300];
            low = new double[300];
            close = new double[300];

            int index = 0;

            foreach (lBar bar in histBars[symbolID])
            {
                DateTime t = UnixTimestampToDateTime(bar.date);
                date[index] = t.ToString("yyyyMMdd");
                time[index] = t.ToString("HHmmss");
                open[index] = bar.open;
                high[index] = bar.high;
                low[index] = bar.low;
                close[index] = bar.close;
                index++;
            }
        }

        public void GetRealBarsAsArrays(int symbolID, out string[] date, out string[] time, out double[] open, out double[] high,
            out double[] low, out double[] close)
        {
            date = new string[300];
            time = new string[300];
            open = new double[300];
            high = new double[300];
            low = new double[300];
            close = new double[300];

            int index = 0;

            foreach (lBar bar in realBars[symbolID])
            {
                DateTime t = UnixTimestampToDateTime(bar.date);
                date[index] = t.ToString("yyyyMMdd");
                time[index] = t.ToString("HHmmss");
                open[index] = bar.open;
                high[index] = bar.high;
                low[index] = bar.low;
                close[index] = bar.close;
                index++;
            }
        }

        public void GetLast200Bars(int symbolID, out string[] date, out string[] time, out double[] open, out double[] high,
            out double[] low, out double[] close)
        {
            date = new string[200];
            time = new string[200];
            open = new double[200];
            high = new double[200];
            low = new double[200];
            close = new double[200];

            SortedDictionary<long, lBar> last200 = new SortedDictionary<long, lBar>();


            foreach (lBar item in histBars[symbolID])
            {
                last200.Add(item.date, item);
            }

            lock (realBars[symbolID])
            {
                foreach (lBar item in realBars[symbolID])
                    if (!last200.ContainsKey(item.date))
                        last200.Add(item.date, item);
            }

            List<lBar> temp = Enumerable.Reverse(last200.Values.ToList()).Take(200).Reverse().ToList<lBar>();

            int index = 0;
            foreach (lBar bar in temp)
            {
                DateTime t = UnixTimestampToDateTime(bar.date);
                date[index] = t.ToString("yyyyMMdd");
                time[index] = t.ToString("HHmmss");
                open[index] = bar.open;
                high[index] = bar.high;
                low[index] = bar.low;
                close[index] = bar.close;
                index++;
            }

            lock (realBars[symbolID])
            {
                realBars[symbolID] = temp;
            }

        }
    }
}