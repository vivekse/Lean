using System;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// MACD Trading Strategy
    /// 1. The price should be above 200 EMA
    /// 2. If MACD line crosses above signal line below 0 histogram, go long
    /// 3. If MACD crossover Short happens above 0, go short
    /// 4. Stop Loss is below the pullback of the trend
    /// 5. Profit target is 1.5 times( or 2 times)
    /// </summary>

    public class MacdResearch : QCAlgorithm
    {
        private new const string Symbol = "SPY";
        private MovingAverageConvergenceDivergence _macd;
        private ExponentialMovingAverage _emaSlow;
        private ExponentialMovingAverage _emaFast;
        private ExponentialMovingAverage _emaMedium;
        private Resolution _resolution = Resolution.Minute;
        private RollingWindow<TradeBar> SwingWindow;
        private int SwingWindowSize = 20;
        private decimal SwingHigh;
        private decimal SwingLow;
        private readonly decimal ProfitLossRaito = 1.5m;
        private OrderTicket StopLoss;
        private OrderTicket ProfitTarget;
        private OrderTicket CurrentOrder;
        private readonly decimal MaxStopLossPercent = 1.0m;
        private readonly decimal MinStopLossPercent = 0.6m;

        public override void Initialize()
        {
            SetStartDate(2015, 1, 1);  //Set Start Date
            SetEndDate(2021, 1, 1);  //Set Start Date
            SetCash(100000);             //Set Strategy Cash

            AddEquity(Symbol, _resolution);

            SwingWindow = new RollingWindow<TradeBar>(SwingWindowSize);

            // Define Consolidator
            var fifteenMinute = new TradeBarConsolidator(TimeSpan.FromMinutes(15));
            // register the consolidator to receive data for our 'Symbol'
            SubscriptionManager.AddConsolidator(Symbol, fifteenMinute);

            // attach our 15 minute event handler, the 'OnFifteenMinuteData' will be called
            // at 9:45, 10:00, 10:15, ect... until 4:00pm
            fifteenMinute.DataConsolidated += OnFifteenMinuteData;

            _macd = new MovingAverageConvergenceDivergence(12, 26, 9);
            _emaSlow = new ExponentialMovingAverage(200);
            _emaFast = new ExponentialMovingAverage(12);
            _emaMedium = new ExponentialMovingAverage(36);

            SetWarmUp(TimeSpan.FromDays(210));
        }

        public void OnFifteenMinuteData(object sender, TradeBar bar)
        {
            // update our indicators
            _macd.Update(Time, bar.Close);
            _emaSlow.Update(Time, bar.Close);
            _emaFast.Update(Time, bar.Close);
            _emaMedium.Update(Time, bar.Close);
            SwingWindow.Add(bar);

            if (IsWarmingUp) return;

            if (!_macd.IsReady) return;

            if (!_emaSlow.IsReady) return;

            // Reset Swing High and Low
            SwingHigh = SwingWindow[0].High;
            SwingLow = SwingWindow[0].Low;

            var currentPrice = bar.Close;


            // Calculate Swing High and Low based on current rolling window
            for (int i = 0; i < SwingWindowSize; i++)
            {
                //Debug($"i={i}, SwingHigh={SwingHigh}, SwingLow={SwingLow}");
                if (SwingWindow[i].High > SwingHigh)
                    SwingHigh = SwingWindow[i].High;
                if (SwingWindow[i].Low < SwingLow)
                    SwingLow = SwingWindow[i].Low;
            }

            var holding = Portfolio[Symbol];

            var signalDeltaPercent = (_macd - _macd.Signal) / _macd.Fast;
            const decimal emaTolerance = 0.001m;
            if (holding.Quantity == 0)// No positions taken yet, ready to take
            {
                var quantity = (int)(Portfolio.Cash / currentPrice);
                // if our macd is greater than our signal, then let's go long
                if (_macd > _macd.Signal && _emaFast > _emaMedium * (1 + emaTolerance) && _emaFast > _emaSlow && _emaMedium > _emaSlow)
                {
                    // Long Order
                    CurrentOrder = MarketOrder(Symbol, quantity);
                    //SetHoldings(Symbol, 1.0);
                    Debug($"LONG Time: {Time}, SPY Close:{Securities[Symbol].Close}, EMA Slow: {_emaSlow}, MACD: {_macd}, Signal: {_macd.Signal}, SignalDeltaPercent: {signalDeltaPercent}");

                    // Calculate Long Stop Loss
                    var stopLoss = CalculateLongStopLoss(currentPrice, SwingLow, MaxStopLossPercent, MinStopLossPercent);

                    // Set Stop Loss Order
                    StopLoss = StopMarketOrder(Symbol, -quantity, stopLoss);

                    // Set Profit Target
                    ProfitTarget = LimitOrder(Symbol, -quantity, currentPrice + ((currentPrice - stopLoss) * ProfitLossRaito));
                }
                // of our macd is less than our signal, then let's go short
                else if (_macd < _macd.Signal && _emaFast < _emaMedium * (1 + emaTolerance) && _emaFast < _emaSlow && _emaMedium < _emaSlow)
                {


                    // Short Order
                    CurrentOrder = MarketOrder(Symbol, -quantity);

                    //SetHoldings(Symbol, -1.0);
                    Debug($"SHORT Time: {Time}, SPY Close:{Securities[Symbol].Close}, EMA Slow: {_emaSlow}, MACD: {_macd}, Signal: {_macd.Signal}, SignalDeltaPercent: {signalDeltaPercent}");

                    // Calculate Short Stop Loss
                    var stopLoss = CalculateShortStopLoss(currentPrice, SwingHigh, MaxStopLossPercent, MinStopLossPercent);

                    // Set Stop Loss Order
                    StopLoss = StopMarketOrder(Symbol, quantity, stopLoss);

                    // Set Profit Target
                    ProfitTarget = LimitOrder(Symbol, quantity, currentPrice - ((stopLoss - currentPrice) * ProfitLossRaito));
                }
            }
            //else // We have holdings, looking to liquidate
            //{


            //    if (holding.Quantity > 0 && _macd < _macd.Signal)
            //    {
            //        Liquidate(Symbol);
            //        Debug($"LIQUIDATE Time: {Time}, SPY Close:{Securities[Symbol].Close}, EMA Slow: {_emaSlow}, MACD: {_macd}, Signal: {_macd.Signal}, SignalDeltaPercent: {signalDeltaPercent}");
            //    }
            //    else if (holding.Quantity < 0 && _macd > _macd.Signal)
            //    {
            //        Liquidate(Symbol);
            //        Debug($"LIQUIDATE Time: {Time}, SPY Close:{Securities[Symbol].Close}, EMA Slow: {_emaSlow}, MACD: {_macd}, Signal: {_macd.Signal}, SignalDeltaPercent: {signalDeltaPercent}");
            //    }
            //}
        }

        // If the StopLoss or ProfitTarget is filled, cancel the other
        // If you don't do this, then  the ProfitTarget or StopLoss order will remain outstanding
        // indefinitely, which will cause very bad behaviors in your algorithm
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            // Ignore OrderEvents that are not closed
            if (!orderEvent.Status.IsClosed())
            {
                return;
            }

            // Defensive check
            if (ProfitTarget == null || StopLoss == null)
            {
                return;
            }

            var filledOrderId = orderEvent.OrderId;

            // If the ProfitTarget order was filled, close the StopLoss order
            if (ProfitTarget.OrderId == filledOrderId)
            {
                StopLoss.Cancel();
            }

            // If the StopLoss order was filled, close the ProfitTarget
            if (StopLoss.OrderId == filledOrderId)
            {
                ProfitTarget.Cancel();
            }

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

        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// Slice object keyed by symbol containing the stock data
        public override void OnData(Slice data)
        {

        }
    }
}
