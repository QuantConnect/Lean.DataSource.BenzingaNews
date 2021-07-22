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

using Newtonsoft.Json;
using System;

namespace QuantConnect.DataSource.DataQueueHandlers
{
    /// <summary>
    /// Message received after a <see cref="BenzingaPingMessage"/> request
    /// </summary>
    public class BenzingaPongMessage : BenzingaPingMessage
    {
        // Raw response received from server
        [JsonProperty("serverTime")]
        private string _rawServerTime;
        private DateTime _serverTime;

        /// <summary>
        /// Server time (UTC) that the server received the ping message
        /// </summary>
        public DateTime ServerTime
        {
            get
            {
                if (_serverTime == default(DateTime) && !string.IsNullOrEmpty(_rawServerTime))
                {
                    _serverTime = BenzingaNewsLiveJsonConverter.NormalizeUtcDateTime(_rawServerTime);
                }

                return _serverTime;
            }
        }
    }
}
