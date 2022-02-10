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
using System.Linq;
using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Packets;
using Newtonsoft.Json.Linq;
using QuantConnect.Logging;
using QuantConnect.Interfaces;
using QuantConnect.Brokerages;
using QuantConnect.Configuration;
using System.Collections.Generic;

namespace QuantConnect.DataSource.DataQueueHandlers
{
    public class BenzingaDataQueueHandler : IDataQueueHandler
    {
        private readonly static string _endpoint = Config.Get("benzinga-endpoint", "wss://api.benzinga.com/api/v1/news/stream");
        private readonly static string _key = Config.Get("benzinga-key");

        private readonly IDataAggregator _dataAggregator;
        private readonly WebSocketClientWrapper _clientWrapper = new();
        private  readonly BenzingaNewsLiveJsonConverter _liveJsonConverter = new();

        /// <summary>
        /// Returns whether the data provider is connected
        /// </summary>
        /// <returns>True if the data provider is connected</returns>
        public bool IsConnected => _clientWrapper.IsOpen;

        /// <summary>
        /// Creates a new instance of Benzinga's <see cref="IDataQueueHandler"/> implementation.
        /// A new background thread is spawned on class creation to collect data in the background.
        /// </summary>
        public BenzingaDataQueueHandler()
        {
            _dataAggregator = Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(
                Config.Get("data-aggregator", "QuantConnect.Data.Common.CustomDataAggregator"), forceTypeNameOnExisting: false);

            if (string.IsNullOrEmpty(_key))
            {
                throw new ArgumentException("'benzinga-key' is required configuration value");
            }
            _clientWrapper.Message += OnClientWrapperOnMessage;
            _clientWrapper.Initialize($"{_endpoint}?token={_key}");
            _clientWrapper.Connect();
        }

       private void OnClientWrapperOnMessage(object sender, WebSocketMessage message)
        {
            try
            {
                var textMessage = message.Data as WebSocketClientWrapper.TextMessage;
                if (Log.DebuggingEnabled)
                {
                    Log.Debug($"BenzingaDataQueueHandler.OnClientWrapperOnMessage(): new message: {textMessage.Message}");
                }
                var jObject = JObject.Parse(textMessage.Message);

                var kind = jObject["kind"]?.ToString();
                if(kind != null && kind.Equals("News/v1", StringComparison.InvariantCultureIgnoreCase))
                {
                    var data = jObject["data"]?.ToString();
                    if (data != null)
                    {
                        var newsData = JsonConvert.DeserializeObject<BenzingaNews>(data, _liveJsonConverter);
                        if (newsData == null)
                        {
                            return;
                        }
                        var symbolsToEmit = newsData.Symbols.Select(s => Symbol.CreateBase(typeof(BenzingaNews), s, Market.USA));

                        foreach (var symbol in symbolsToEmit)
                        {
                            var clone = newsData.Clone();
                            clone.Symbol = symbol;

                            _dataAggregator.Update((BenzingaNews)clone);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            return null;
        }

        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        /// <param name="dataConfig">Subscription config to be removed</param>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
        }

        /// <summary>
        /// Shuts down the background thread and sets the
        /// cancellation token to a canceled state.
        /// </summary>
        public void Dispose()
        {
            _clientWrapper.Close();
        }
    }
}
