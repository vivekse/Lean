using QuantConnect.Data.Consolidators;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using System;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Goal is to implement the Macd strategy and validate the code behaves as expected
    /// 1. Buy when Macd crosses over Signal line
    /// 2. Sell when MACD crossed below Signal line
    /// </summary>
    public class MacdBasicStrategy : QCAlgorithm
    {
        private Resolution _resolution = Resolution.Minute;
        private readonly string Ticker = "BBY";
        private MovingAverageConvergenceDivergence _macd;
        private OrderTicket CurrentOrder;

        public override void Initialize()
        {
            //base.Initialize();
            SetCash(10000);

            SetStartDate(2021,05,01);
            SetEndDate(2021, 06, 15);
            AddEquity(Ticker, _resolution);

            // Define Consolidator - We want to consolidate 4 hours but by default
            // the QuantConnect consollidates at 12 PM and 4 PM, we want to consolidate 
            // at 1:30 PM and 4 PM
            var thirtyMinuteConsolidator = new TradeBarConsolidator(TimeSpan.FromMinutes(30));

            thirtyMinuteConsolidator.DataConsolidated += ThirtyMinuteConsolidator_DataConsolidated;

            SubscriptionManager.AddConsolidator(Ticker, thirtyMinuteConsolidator);


            _macd = new MovingAverageConvergenceDivergence(12, 26, 9);
            SetWarmUp(TimeSpan.FromDays(40));
        }

        private void ThirtyMinuteConsolidator_DataConsolidated(object sender, Data.Market.TradeBar tradeBar)
        {
            // We are getting 30 minute data, but we want to consolidate on 4 hour, i.e. 2 candles every day
            // ending 1:30 PM and 4 PM 
            if ((tradeBar.EndTime.Hour == 13 && tradeBar.EndTime.Minute > 0) || (tradeBar.EndTime.Hour == 16 && tradeBar.EndTime.Minute == 0))
            {
                _macd.Update(Time, tradeBar.Close);

                if (IsWarmingUp)
                {
                    return;
                }

                if (!_macd.IsReady)
                {
                    return;
                }


                var currentPrice = tradeBar.Close;
                var holding = Portfolio[Ticker];

                // If position not open and MACD crossed over signal
                if (holding.Quantity == 0 && _macd > _macd.Signal)
                {
                    var quantity = (int) (Portfolio.Cash / currentPrice);
                    CurrentOrder = MarketOrder(Ticker, quantity);
                    Debug(
                        $"BUY Time: {Time}, {Ticker} Close:{Securities[Ticker].Close}, MACD: {_macd}, Signal: {_macd.Signal}");
                }

                // If position is open and Signal line crosses over MACD, Liquidate
                if (holding.Quantity > 0 && _macd.Signal > _macd)
                {
                    Liquidate(Ticker);
                    Debug(
                        $"Liquidate Time: {Time}, {Ticker} Close:{Securities[Ticker].Close}, MACD: {_macd}, Signal: {_macd.Signal}");
                }
            }
        }
    }
}
