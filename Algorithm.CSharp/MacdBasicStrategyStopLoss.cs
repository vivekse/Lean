using QuantConnect.Data.Consolidators;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using System;
using QuantConnect.Data.Market;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Goal is to implement the Macd strategy and validate the code behaves as expected
    /// 1. Buy when Macd crosses over Signal line
    /// 2. Sell when MACD crossed below Signal line
    /// </summary>
    public class MacdBasicStrategyStopLoss : QCAlgorithm
    {
        private Resolution _resolution = Resolution.Minute;
        private readonly string Ticker = "BBY";
        private MovingAverageConvergenceDivergence _macd;
        private OrderTicket CurrentOrder;
        private OrderTicket StopLoss;
        private OrderTicket ProfitTarget;
        private RollingWindow<TradeBar> SwingWindow;
        private int SwingWindowSize = 20;
        private decimal SwingHigh;
        private decimal SwingLow;


        public override void Initialize()
        {
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


            SwingWindow = new RollingWindow<TradeBar>(SwingWindowSize);

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
                SwingWindow.Add(tradeBar);
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

                // Reset Swing High and Low
                SwingHigh = SwingWindow[0].High;
                SwingLow = SwingWindow[0].Low;


                // Calculate Swing High and Low based on current rolling window
                for (var i = 0; i < SwingWindowSize; i++)
                {
                    if (SwingWindow[i].High > SwingHigh)
                    {
                        SwingHigh = SwingWindow[i].High;
                    }

                    if (SwingWindow[i].Low < SwingLow)
                    {
                        SwingLow = SwingWindow[i].Low;
                    }
                }

                // If position not open and MACD crossed over signal
                if (holding.Quantity == 0 && _macd > _macd.Signal)
                {
                    var quantity = (int) (Portfolio.Cash / currentPrice);
                    CurrentOrder = MarketOrder(Ticker, quantity);
                    Debug(
                        $"BUY Time: {Time}, {Ticker} Close:{Securities[Ticker].Close}, MACD: {_macd}, Signal: {_macd.Signal}");

                    var stopLoss = CalculateLongStopLoss(currentPrice, 1);

                    // Set Stop Loss Order
                    StopLoss = StopMarketOrder(Ticker, -quantity, stopLoss);



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

        private decimal CalculateLongStopLoss(decimal swingLow, decimal percentBelowSwingLow)
        {
            return swingLow - swingLow * percentBelowSwingLow / 100;
        }

        private decimal CalculateShortStopLoss(decimal swingHigh, decimal percentAboveSwingLow)
        {
            return swingHigh + swingHigh * percentAboveSwingLow / 100;
        }

        private decimal CalculateLongStopLoss(decimal currentPrice, decimal swingLow, decimal maxStopLossPercent, decimal minStopLossPercent)
        {
            if ((currentPrice - swingLow) * 100 / currentPrice > maxStopLossPercent)
            {
                return currentPrice - (currentPrice * maxStopLossPercent / 100);
            }
            else if ((currentPrice - swingLow) * 100 / currentPrice < minStopLossPercent)
            {
                return currentPrice - (currentPrice * minStopLossPercent / 100);
            }
            return swingLow;
        }

        private decimal CalculateShortStopLoss(decimal currentPrice, decimal swingHigh, decimal maxStopLossPercent, decimal minStopLossPercent)
        {
            if ((swingHigh - currentPrice) * 100 / currentPrice > maxStopLossPercent)
            {
                return currentPrice + (currentPrice * maxStopLossPercent / 100);
            }
            if ((swingHigh - currentPrice) * 100 / currentPrice < minStopLossPercent)
            {
                return currentPrice + (currentPrice * minStopLossPercent / 100);
            }
            return swingHigh;
        }
    }
}
