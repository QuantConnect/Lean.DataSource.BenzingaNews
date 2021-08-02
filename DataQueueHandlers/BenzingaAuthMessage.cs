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
    /// Authentication message to send to Benzinga TCP stream (https://docs.benzinga.io/benzinga/newsfeed-tcp-v1-1.html)
    /// </summary>
    public class BenzingaAuthMessage : IBenzingaMessage
    {
        /// <summary>
        /// Username to authenticate with
        /// </summary>
        [JsonProperty("username")]
        public string Username { get; set; }

        /// <summary>
        /// Key to authenticate with
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; }

        /// <summary>
        /// Creates an instance of the class to authenticate with
        /// </summary>
        /// <param name="user">User to authenticate as</param>
        /// <param name="key">Key to authenticate with</param>
        public BenzingaAuthMessage(string user, string key)
        {
            Username = user;
            Key = key;
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
