﻿/* Copyright (C) 2019 Interactive Brokers LLC. All rights reserved. This code is subject to the terms
 * and conditions of the IB API Non-Commercial License or the IB API Commercial License, as applicable. */
using System;

namespace IBSampleApp.messages
{
    public class RealTimeBarMessage : HistoricalDataMessage
    {
        private long timestamp;
        private long longVolume;

        public long LongVolume
        {
            get { return longVolume; }
            set { longVolume = value; }
        }

        public long Timestamp
        {
            get { return timestamp; }
            set { timestamp = value; }
        }

        public RealTimeBarMessage(int reqId, long date, double open, double high, double low, double close, long volume, double WAP, int count)
            : base(reqId, new IBApi.Bar(date.ToString(), open, high, low, close, -1, count, WAP))
        {
            Timestamp = date;
            LongVolume = volume;
            //UnixTimestampToDateTime(date).ToString("yyyyMMdd hh:mm:ss")
        }

        static DateTime UnixTimestampToDateTime(long unixTimestamp)
        {
            DateTime unixBaseTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return unixBaseTime.AddSeconds(unixTimestamp);
        }
    }
}