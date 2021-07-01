/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using System.Collections.Generic;

namespace QuantConnect.Tests.ToolBox
{
    [TestFixture]
    public class LeanDataWriterTests
    {
        private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private Symbol _forex;
        private Symbol _cfd;
        private Symbol _equity;
        private Symbol _crypto;
        private DateTime _date;

        [OneTimeSetUp]
        public void Setup()
        {
            _forex = Symbol.Create("EURUSD", SecurityType.Forex, Market.FXCM);
            _cfd = Symbol.Create("BCOUSD", SecurityType.Cfd, Market.Oanda);
            _equity = Symbol.Create("spy", SecurityType.Equity, Market.USA);
            _date = Parse.DateTime("3/16/2017 12:00:00 PM");
            _crypto = Symbol.Create("BTCUSD", SecurityType.Crypto, Market.GDAX);
        }

        private List<Tick> GetTicks(Symbol sym)
        {
            return new List<Tick>()
            {
                new Tick(Parse.DateTime("3/16/2017 12:00:00 PM"), sym, 1.0m, 2.0m),
                new Tick(Parse.DateTime("3/16/2017 12:00:01 PM"), sym, 3.0m, 4.0m),
                new Tick(Parse.DateTime("3/16/2017 12:00:02 PM"), sym, 5.0m, 6.0m),
            };
        }

        private List<QuoteBar> GetQuoteBars(Symbol sym)
        {
            return new List<QuoteBar>()
            {
                new QuoteBar(Parse.DateTime("3/16/2017 12:00:00 PM"), sym, new Bar(1m, 2m, 3m, 4m),  1, new Bar(5m, 6m, 7m, 8m),  2),
                new QuoteBar(Parse.DateTime("3/16/2017 12:00:01 PM"), sym, new Bar(11m, 21m, 31m, 41m),  3, new Bar(51m, 61m, 71m, 81m), 4),
                new QuoteBar(Parse.DateTime("3/16/2017 12:00:02 PM"), sym, new Bar(10m, 20m, 30m, 40m),  5, new Bar(50m, 60m, 70m, 80m),  6),
            };
        }

        [Test]
        public void LeanDataWriter_CanWriteForex()
        {
            var filePath = LeanData.GenerateZipFilePath(_dataDirectory, _forex, _date, Resolution.Second, TickType.Quote);

            var leanDataWriter = new LeanDataWriter(Resolution.Second, _forex, _dataDirectory, TickType.Quote);
            leanDataWriter.Write(GetQuoteBars(_forex));

            Assert.IsTrue(File.Exists(filePath));
            Assert.IsFalse(File.Exists(filePath + ".tmp"));

            var data = QuantConnect.Compression.Unzip(filePath);

            Assert.AreEqual(data.First().Value.Count(), 3);
        }

        [TestCase(SecurityType.FutureOption)]
        [TestCase(SecurityType.Future)]
        [TestCase(SecurityType.Option)]
        public void LeanDataWriter_CanWriteZipWithMultipleContracts(SecurityType securityType)
        {
            Symbol contract1;
            Symbol contract2;
            if (securityType == SecurityType.Future)
            {
                contract1 = Symbol.CreateFuture(Futures.Indices.SP500EMini, Market.CME, new DateTime(2020, 02, 01));
                contract2 = Symbol.CreateFuture(Futures.Indices.SP500EMini, Market.CME, new DateTime(2020, 03, 01));
            }
            else if (securityType == SecurityType.Option)
            {
                contract1 = Symbol.CreateOption("AAPL", Market.USA, OptionStyle.American, OptionRight.Call, 1, new DateTime(2020, 02, 01));
                contract2 = Symbol.CreateOption("AAPL", Market.USA, OptionStyle.American, OptionRight.Call, 1, new DateTime(2020, 03, 01));
            }
            else if (securityType == SecurityType.FutureOption)
            {
                contract1 = Symbol.CreateOption(Futures.Indices.SP500EMini, Market.CME, OptionStyle.American, OptionRight.Call, 1, new DateTime(2020, 02, 01));
                contract2 = Symbol.CreateOption(Futures.Indices.SP500EMini, Market.CME, OptionStyle.American, OptionRight.Call, 1, new DateTime(2020, 03, 01));
            }
            else
            {
                throw new NotImplementedException($"{securityType} not implemented!");
            }

            var filePath1 = LeanData.GenerateZipFilePath(_dataDirectory, contract1, _date, Resolution.Second, TickType.Quote);
            var leanDataWriter1 = new LeanDataWriter(Resolution.Second, contract1, _dataDirectory, TickType.Quote);
            leanDataWriter1.Write(GetQuoteBars(contract1));

            var filePath2 = LeanData.GenerateZipFilePath(_dataDirectory, contract2, _date, Resolution.Second, TickType.Quote);
            var leanDataWriter2 = new LeanDataWriter(Resolution.Second, contract2, _dataDirectory, TickType.Quote);
            leanDataWriter2.Write(GetQuoteBars(contract2));

            Assert.AreEqual(filePath1, filePath2);
            Assert.IsTrue(File.Exists(filePath1));
            Assert.IsFalse(File.Exists(filePath1 + ".tmp"));

            var data = QuantConnect.Compression.Unzip(filePath1).ToDictionary(x => x.Key, x => x.Value.ToList());
            Assert.AreEqual(2, data.Count);
            Assert.That(data.Values, Has.All.Count.EqualTo(3));
        }

        [Test]
        public void LeanDataWriter_CanWriteCfd()
        {
            var filePath = LeanData.GenerateZipFilePath(_dataDirectory, _cfd, _date, Resolution.Minute, TickType.Quote);

            var leanDataWriter = new LeanDataWriter(Resolution.Minute, _cfd, _dataDirectory, TickType.Quote);
            leanDataWriter.Write(GetQuoteBars(_cfd));

            Assert.IsTrue(File.Exists(filePath));
            Assert.IsFalse(File.Exists(filePath + ".tmp"));

            var data = QuantConnect.Compression.Unzip(filePath);

            Assert.AreEqual(data.First().Value.Count(), 3);
        }

        [Test]
        public void LeanDataWriter_CanWriteEquity()
        {
            var filePath = LeanData.GenerateZipFilePath(_dataDirectory, _equity, _date, Resolution.Tick, TickType.Trade);

            var leanDataWriter = new LeanDataWriter(Resolution.Tick, _equity, _dataDirectory);
            leanDataWriter.Write(GetTicks(_equity));

            Assert.IsTrue(File.Exists(filePath));
            Assert.IsFalse(File.Exists(filePath + ".tmp"));

            var data = QuantConnect.Compression.Unzip(filePath);

            Assert.AreEqual(data.First().Value.Count(), 3);
        }

        [Test]
        public void LeanDataWriter_CanWriteCrypto()
        {
            var filePath = LeanData.GenerateZipFilePath(_dataDirectory, _crypto, _date, Resolution.Second, TickType.Quote);

            var leanDataWriter = new LeanDataWriter(Resolution.Second, _crypto, _dataDirectory, TickType.Quote);
            leanDataWriter.Write(GetQuoteBars(_crypto));

            Assert.IsTrue(File.Exists(filePath));
            Assert.IsFalse(File.Exists(filePath + ".tmp"));

            var data = QuantConnect.Compression.Unzip(filePath);

            Assert.AreEqual(data.First().Value.Count(), 3);
        }
    }
}
