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
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Securities.Volatility;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Demonstrates the usage of the <see cref="IndicatorVolatilityModel"/>.
    /// The algorithm uses a custom indicator (StandardDeviation over SMA)
    /// as a volatility estimator, while correctly handling corporate actions
    /// such as splits and dividends to avoid price discontinuities.
    /// </summary>
    public class IndicatorVolatilityModelAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const int Period = 7;
        private const DataNormalizationMode Mode = DataNormalizationMode.Raw;

        private Symbol _aapl;
        private IIndicator _indicator;
        private int _splitsAndDividendsCount;
        private bool _volatilityChecked;

        public override void Initialize()
        {
            SetStartDate(2014, 1, 1);
            SetEndDate(2014, 12, 31);
            SetCash(100000);

            var equity = AddEquity("AAPL", Resolution.Daily, dataNormalizationMode: Mode);
            _aapl = equity.Symbol;

            var std = new StandardDeviation(Period);
            var mean = new SimpleMovingAverage(Period);
            _indicator = std.Over(mean);

            // Attach custom volatility model
            equity.SetVolatilityModel(new IndicatorVolatilityModel(_indicator, (_, data, _) =>
            {
                if (data.Price > 0)
                {
                    std.Update(data.Time, data.Price);
                    mean.Update(data.Time, data.Price);
                }
            }));
        }

        public override void OnData(Slice slice)
        {
            if (slice.Splits.ContainsKey(_aapl) || slice.Dividends.ContainsKey(_aapl))
            {
                _splitsAndDividendsCount++;

                // Reset and warm up indicator to prevent false spikes in volatility
                _indicator.Reset();
                var equity = Securities[_aapl];
                var volModel = equity.VolatilityModel as IndicatorVolatilityModel;
                volModel.WarmUp(this, equity, equity.Resolution, Period, Mode);
            }
        }

        public override void OnEndOfDay(Symbol symbol)
        {
            if (symbol != _aapl || !_indicator.IsReady)
                return;

            _volatilityChecked = true;

            // Sanity check — volatility should stay stable (< 0.05)
            var volatility = Securities[_aapl].VolatilityModel.Volatility;
            if (volatility <= 0 || volatility > 0.05m)
            {
                throw new RegressionTestException(
                    $"Expected volatility < 0.05 (no large jumps from corporate actions), but got {volatility}"
                );
            }
        }

        public override void OnEndOfAlgorithm()
        {
            if (_splitsAndDividendsCount == 0)
                throw new RegressionTestException("Expected to get at least one split or dividend event");

            if (!_volatilityChecked)
                throw new RegressionTestException("Expected to check volatility at least once");
        }

        /// <summary>
        /// Used by the regression testing framework to verify this algorithm’s correctness.
        /// </summary>
        public bool CanRunLocally => true;

        public List<Language> Languages => new() { Language.CSharp };

        public long DataPoints => 2021;

        public int AlgorithmHistoryDataPoints => 42;

        public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Completed;

        public Dictionary<string, string> ExpectedStatistics => new()
        {
            {"Total Orders", "0"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "0%"},
            {"Drawdown", "0%"},
            {"Expectancy", "0"},
            {"Start Equity", "100000"},
            {"End Equity", "100000"},
            {"Net Profit", "0%"},
            {"Sharpe Ratio", "0"},
            {"Sortino Ratio", "0"},
            {"Probabilistic Sharpe Ratio", "0%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0"},
            {"Beta", "0"},
            {"Annual Standard Deviation", "0"},
            {"Annual Variance", "0"},
            {"Information Ratio", "-1.025"},
            {"Tracking Error", "0.094"},
            {"Treynor Ratio", "0"},
            {"Total Fees", "$0.00"},
            {"Estimated Strategy Capacity", "$0"},
            {"Lowest Capacity Asset", ""},
            {"Portfolio Turnover", "0%"},
            {"Drawdown Recovery", "0"},
            {"OrderListHash", "d41d8cd98f00b204e9800998ecf8427e"}
        };
    }
}
