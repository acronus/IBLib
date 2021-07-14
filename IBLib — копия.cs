using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Reflection;
using IBApi;
using IBSampleApp.messages;
using static IBLib.Api;
using System.Threading.Tasks;
using System.Windows;
using System.Collections;

namespace IBLib
{
    [Guid("EAA4976A-45C3-4BC5-BC0B-E474F4C3C23F"),
    ComVisible(true)]
    public interface IApi
    {
        void ConnectApi(string host = "", int port = 7497, int clientID = 0);
        bool IsConnected();
        void DisconnectApi();
        void SetMarketDataType(int mtype);
        void AskForPrice(int symbolID, string genericTickList = "233");
        void StopAskingPrice();
        void AskForRTBar(int symbolID, string WhatToShow);
        void StopRTBar();
        string GetBarsAsText(int symbolID);
        void GetBarsAsRecord(int symbolID, out RTbars rtbars);
        void GetBarsAsArrays(int symbolID, out string[] date, out string[] time, out double[] open, out double[] high,
             out double[] low, out double[] close);
        int GetNewOrderID();
        void PlaceOrderByRecord(int orderID, Contract contract, Order order);
        void PlaceOrder(string Symbol, string SecType, string Exchange, string Currency,
            string LocalSymbol, bool IncludeExpired, string LastTradeDateOrContractMonth,
            string PrimaryExch, string PUTorCALL, double Strike, string Multiplier,
            int OrderID, string Action, string OrderType, int TotalQuantity, double LmtPrice);
        void CancelOrder(int id);
        void CancelAllOrders();
        void CancelOrdersBySymbol(string symbol);
        void GetBuySellCount(string symbol, out int buy, out int sell);
        void AskPositions();
        Order GetOrder(string Action, string OrderType, double TotalQuantity, double LmtPrice);
        Contract GetContract(string Symbol, string SecType, string Exchange, string Currency,
        string LocalSymbol, bool IncludeExpired, string LastTradeDateOrContractMonth, string PrimaryExch,
        string PUTorCALL, double Strike, string Multiplier);
        Symbol getAll(int id);
        void getContractDetails(Contract c);
        string getDict();
        void AddSymbol(int id, string name, Contract contract);
    }

    [Guid("EAA4976A-45C3-4BC5-BC0B-E474F4C3C13F"),
    ClassInterface(ClassInterfaceType.None),
    ComVisible(true)]
    public class Api : IApi
    {
        static SynchronizationContext sc = new SynchronizationContext();
        public Samples.EWrapperImpl ibClient = new Samples.EWrapperImpl(sc);

        public int clientID;
        public int baseID = 1000000;
        public int ReqId = 1;

        string symbolToCancel = "";
        string symbolToCntOrders = "";

        int cntBUYorders = -1;
        int cntSELLorders = -1;

        int tBUY = 0;
        int tSELL = 0;

        Dictionary<int, string> contracts = new Dictionary<int, string>();

        //public TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

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
            public Contract contract;
            public ContractDetails contractDetails;
            public double lastprice;
  
            public int priceReqId;
            public int RTbarReqId;
        }

        public struct SymbolUtility
        {
            public int id;

            public Contract contract;
            public ContractDetails contractDetails;

            public int priceReqId;
            public int RTbarReqId;
            public bool startMinBar;
            public bool endMinBar;
            public mBar[] bars;
            public mBar currentBar;
            public mBar[] minbars;
        }


        Symbol[] all = new Symbol[1000];
        SymbolUtility[] uall = new SymbolUtility[1000];

        public Api()
        {
            ibClient.TickPrice += IbClient_TickPrice;
            ibClient.RealtimeBar += IbClient_RealtimeBar;
            ibClient.OpenOrder += IbClient_OpenOrder;
            ibClient.OpenOrderEnd += IbClient_OpenOrderEnd;
            ibClient.Position += IbClient_Position;
            ibClient.PositionEnd += IbClient_PositionEnd;
            ibClient.ContractDetails += IbClient_ContractDetails;
        }

        private void IbClient_ContractDetails(ContractDetailsMessage contract)
        {
            contracts.Add(contract.ContractDetails.Contract.ConId, contract.ContractDetails.Contract.Symbol);
        }

        private void IbClient_PositionEnd()
        {
            //tcs.SetResult(true);
        }

        private void IbClient_Position(PositionMessage Position)
        {

        }

        private void IbClient_OpenOrderEnd()
        {
            //symbolToCancel = "";

            /*
            cntBUYorders = tBUY;
            cntSELLorders = tSELL;
            symbolToCntOrders = "";
            tBUY = 0;
            tSELL = 0;*/

            //tcs.SetResult(true);
        }

        private void IbClient_OpenOrder(OpenOrderMessage OpenOrder)
        {
            // Count BUY and SELL
            if (OpenOrder.Contract.Symbol == symbolToCntOrders)
            {
                if (OpenOrder.Order.Action == "BUY") tBUY++; else tSELL++;
            }

            // Cancel orders
            if (OpenOrder.Contract.Symbol == symbolToCancel)
            {
                ibClient.ClientSocket.cancelOrder(OpenOrder.OrderId);
            }
        }

        private void IbClient_RealtimeBar(RealTimeBarMessage bar)
        {
            foreach (Symbol s in all)
            {
                if (s.RTbarReqId == bar.RequestId)
                {

                    MessageBox.Show(bar.ToString());

                    int symbolID = s.id;

                    DateTime t = DateTime.ParseExact(bar.Date, "yyyyMMdd HH:mm:ss",
                                                CultureInfo.InvariantCulture);

                    int index = int.Parse(t.ToString("ss")) / 5;

                    uall[symbolID].bars[index].date = t.ToString("yyyyMMdd");
                    uall[symbolID].bars[index].time = t.ToString("HHmmss");
                    uall[symbolID].bars[index].open = bar.Open;
                    uall[symbolID].bars[index].high = bar.High;
                    uall[symbolID].bars[index].low = bar.Low;
                    uall[symbolID].bars[index].close = bar.Close;

                    uall[symbolID].currentBar = uall[symbolID].bars[index];

                    if (index == 0)
                    {
                        uall[symbolID].startMinBar = true;
                    }
                    if ((index == 11) && (uall[symbolID].startMinBar == true))
                    {
                        uall[symbolID].endMinBar = true;
                    }

                    if ((uall[symbolID].startMinBar) && (uall[symbolID].endMinBar))
                    {
                        mBar bar1m;

                        bar1m.date = uall[symbolID].bars[0].date;
                        bar1m.time = uall[symbolID].bars[0].time;

                        double max = 0;
                        double min = uall[symbolID].bars[0].low;

                        for (int i = 0; i < 12; i++)
                        {
                            if (uall[symbolID].bars[i].high > max) max = uall[symbolID].bars[i].high;
                            if (uall[symbolID].bars[i].low < min) min = uall[symbolID].bars[i].low;
                        }

                        bar1m.open = uall[symbolID].bars[0].open;
                        bar1m.high = max;
                        bar1m.low = min;
                        bar1m.close = uall[symbolID].bars[11].close;

                        while (uall[symbolID].minbars.Length > 8)
                        {
                            uall[symbolID].minbars = uall[symbolID].minbars.Skip(1).ToArray();
                        }
                        uall[symbolID].minbars[uall[symbolID].minbars.Length] = bar1m;

                        uall[symbolID].startMinBar = false;
                        uall[symbolID].endMinBar = false;
                    }
                }
            }
        }

        private void IbClient_TickPrice(TickPriceMessage dataMessage)
        {
            if ((dataMessage.Field == TickType.LAST) || (dataMessage.Field == TickType.DELAYED_LAST))
            {

                //MessageBox.Show( all[1].priceReqId.ToString() );

                foreach (Symbol s in all)
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
                { IsBackground = true }.Start();

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

            //WhatToShow BID_ASK MIDPOINT etc
            ibClient.ClientSocket.reqRealTimeBars(all[symbolID].RTbarReqId , all[symbolID].contract, 5, WhatToShow, true, DataOptions);
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

            currentBarText = uall[symbolID].currentBar.date + ";" + uall[symbolID].currentBar.time + ";" + uall[symbolID].currentBar.open + ";"
                    + uall[symbolID].currentBar.high + ";" + uall[symbolID].currentBar.low + ";" + uall[symbolID].currentBar.close + ";";

            if (currentBarText.Length < 10) return "";

            string minBarsText = "";

            foreach (mBar minbar in uall[symbolID].minbars)
            {
                minBarsText = minbar.date + ";" + minbar.time + ";" + minbar.open + ";"
                + minbar.high + ";" + minbar.low + ";" + minbar.close + ";" + minBarsText;
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
            low  = new double[10];
            close = new double[10];

            date[0] = uall[symbolID].currentBar.date;
            time[0] = uall[symbolID].currentBar.time;
            open[0] = uall[symbolID].currentBar.open;
            high[0] = uall[symbolID].currentBar.high;
            low[0]  = uall[symbolID].currentBar.low;
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


        public void PlaceOrderByRecord(int orderID, Contract contract, Order order)
        {
            ibClient.ClientSocket.placeOrder(orderID, contract, order);
            ibClient.NextOrderId++;
        }

        public void PlaceOrder(string Symbol, string SecType, string Exchange, string Currency,
            string LocalSymbol, bool IncludeExpired, string LastTradeDateOrContractMonth,
            string PrimaryExch, string PUTorCALL, double Strike, string Multiplier,
            int OrderID, string Action, string OrderType, int TotalQuantity, double LmtPrice)
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

            Order order = new Order();
            order.Action = Action; //BUY or SELL
            order.OrderType = OrderType; // LMT
            order.TotalQuantity = TotalQuantity;
            order.LmtPrice = LmtPrice;
            order.Tif = "GTC";

            ibClient.ClientSocket.placeOrder(OrderID, contract, order);
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
            symbolToCancel = symbol;
            ibClient.ClientSocket.reqOpenOrders();
        }

        public void GetBuySellCount(string symbol, out int buy, out int sell)
        {
            /* symbolToCntOrders = symbol;
             ibClient.ClientSocket.reqOpenOrders();
             while ((cntBUYorders < 0) && (cntSELLorders < 0)) { }
            */
            buy = cntBUYorders;
            sell = cntSELLorders;
            cntBUYorders = -1;
            cntSELLorders = -1;
        }

        /*
        public async Task reqOpenOrders()
        {
            //ibClient.ClientSocket.reqOpenOrders();
            //await tcs.Task;
            return;
        }*/

        public void AskPositions()
        {
            ibClient.ClientSocket.reqPositions();

        }

        public Order GetOrder(string Action, string OrderType, double TotalQuantity, double LmtPrice)
        {
            Order order = new Order();
            order.Action = Action; //BUY or SELL
            order.OrderType = OrderType; // LMT
            order.TotalQuantity = TotalQuantity;
            order.LmtPrice = LmtPrice;
            order.Tif = "GTC";

            return order;
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

        public Symbol getAll(int id)
        {
            return all[id];
        }

        public void getContractDetails(Contract c)
        {
            ibClient.ClientSocket.reqContractDetails(getReqId(), c);
        }

        public int getReqId()
        {
            return baseID * clientID + ++ReqId;
        }

        public string getDict()
        {
            string txt = "";

            foreach(var d in contracts)
            {
                txt += d.Key + ";" + d.Value + "   ";
            }

            return txt;
        }

        public void AddSymbol(int id, string name, Contract contract)
        {
            Symbol s = new Symbol();
            s.id = id;
            s.name = name;
            s.contract = contract;

            s.priceReqId = getReqId();
            s.RTbarReqId = getReqId();

            SymbolUtility u = new SymbolUtility();
            
            u.id = id;
            u.contract = contract;
            u.priceReqId = s.priceReqId;
            u.RTbarReqId = s.RTbarReqId;
            u.bars = new mBar[12];
            u.minbars = new mBar[9];

            u.startMinBar = false;
            u.endMinBar = false;

            all[id] = s;
            uall[id] = u;
        }
    }
}