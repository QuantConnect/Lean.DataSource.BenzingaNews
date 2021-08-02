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
    public enum BenzingaMessageType
    {
        // Unknown unknowns group
        /// <summary>
        /// Unknown message received
        /// </summary>
        UnknownError,
        /// <summary>
        /// Server received unknown command
        /// </summary>
        UnknownCommand,

        // Connection status group
        /// <summary>
        /// Server is ready for authentication
        /// </summary>
        Ready,
        /// <summary>
        /// Server has accepted our credentials and connected successfully
        /// </summary>
        ConnectionAck,
        /// <summary>
        /// Server has shutdown our connection
        /// </summary>
        Goodbye,

        // Specific errors
        /// <summary>
        /// Invalid key received (i.e. incorrect password)
        /// </summary>
        BadKey,
        /// <summary>
        /// Invalid key format
        /// </summary>
        BadKeyFormat,
        /// <summary>
        /// A duplicate connection was established using the
        /// same credentials that this connection was made with
        /// </summary>
        DuplicateConnection,

        // User actionable events group (messages we can send to server)
        /// <summary>
        /// Authentication/Login
        /// </summary>
        Auth,
        /// <summary>
        /// Ping/heartbeat to ensure connection is active
        /// </summary>
        Ping,

        // Server replies group (messages we receive)
        /// <summary>
        /// Live news article published into the TCP stream
        /// </summary>
        Stream,
        /// <summary>
        /// Response to a <see cref="Ping"/> message sent indicating
        /// that the connection is alive
        /// </summary>
        Pong
    }
}
