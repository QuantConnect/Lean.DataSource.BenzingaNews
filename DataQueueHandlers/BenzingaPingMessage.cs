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
using System.Text;

namespace QuantConnect.DataSource.DataQueueHandlers
{
    /// <summary>
    /// Ping message to send to Benzinga TCP stream (https://docs.benzinga.io/benzinga/newsfeed-tcp-v1-1.html)
    /// </summary>
    public class BenzingaPingMessage : IBenzingaMessage
    {
        /// <summary>
        /// Value contained in the ping request. This value
        /// will be returned in the subsequent <see cref="BenzingaPongMessage"/> response
        /// </summary>
        [JsonProperty("pingTime")]
        public string Value { get; set; }

        /// <summary>
        /// Required for inheritance and Json.NET deserialization
        /// </summary>
        public BenzingaPingMessage()
        {
        }

        /// <summary>
        /// Creates a new instance of the class to send a ping message with
        /// </summary>
        /// <param name="value">Value contained in ping request</param>
        public BenzingaPingMessage(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Creates a new message to send to the Benzinga server
        /// </summary>
        /// <param name="start">String to prepend to the message</param>
        /// <param name="eof">String to append to the message</param>
        /// <returns>Sendable message</returns>
        private string ConstructMessage(string start = null, string eof = null)
        {
            return $"{start}{JsonConvert.SerializeObject(this)}{eof}";
        }

        /// <summary>
        /// Creates a new message to send to the Benzinga server
        /// </summary>
        /// <param name="start">String to prepend to the message</param>
        /// <param name="eof">String to append to the message</param>
        /// <returns>Sendable message usable with Sockets</returns>
        public byte[] ConstructMessageBytes(string start = null, string eof = null)
        {
            return Encoding.UTF8.GetBytes(ConstructMessage(start, eof));
        }
    }
}
