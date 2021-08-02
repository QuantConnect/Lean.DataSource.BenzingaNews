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

namespace QuantConnect.DataSource.DataQueueHandlers
{
    public interface IBenzingaMessage
    {
        /// <summary>
        /// Constructs the message as a byte array (for use in sockets)
        /// </summary>
        /// <param name="start">String to prepend to the message being constructed</param>
        /// <param name="eof">String to append to the message being constructed</param>
        /// <returns>Message sendable to the Benzinga TCP server via a Socket connection</returns>
        byte[] ConstructMessageBytes(string start = null, string eof = null);
    }
}
