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
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.DataSource;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Util;

namespace QuantConnect.DataSource.DataQueueHandlers
{
    public class BenzingaDataQueueHandler : IDataQueueHandler
    {
        /// <summary>
        /// Benzinga's TCP stream end of message delimiter
        /// </summary>
        private const string EOF = "=BZEOT\r\n";

        private readonly static string _host = Config.Get("benzinga-tcp-host", "tcp-v1.benzinga.io");
        private readonly static int _port = Config.GetInt("benzinga-tcp-port", 11337);
        private readonly static string _user = Config.Get("benzinga-tcp-user");
        private readonly static string _key = Config.Get("benzinga-tcp-key");

        private readonly object _subscriptionLock = new object();
        private readonly bool _overrideSubscriptionCheck;
        private readonly RateGate _connectionGate;
        private readonly TimeSpan _heartbeatInterval;
        private readonly TimeSpan _responseTimeout;
        private readonly Thread _tcpThread;
        private readonly CancellationTokenSource _cancellationSource;
        private IDataAggregator _dataAggregator;
        private HashSet<Symbol> _subscriptionSymbols;
        private bool _connected;

        /// <summary>
        /// Creates a new instance of Benzinga's <see cref="IDataQueueHandler"/> implementation.
        /// A new background thread is spawned on class creation to collect data in the background.
        /// </summary>
        /// <param name="dataAggregator">The data aggregator instance</param>
        /// <param name="connectionTimeoutWait">Time to wait between reconnect attempts</param>
        /// <param name="heartbeatInterval">Seconds to wait between sending ping messages</param>
        /// <param name="responseTimeout">Seconds to wait before considering the connection timed out (no response)</param>
        /// <param name="overrideSubscriptionCheck">Allows for any <see cref="Symbol"/> to be added to the active subscriptions</param>
        public BenzingaDataQueueHandler()
        {
            _dataAggregator = Composer.Instance.GetPart<IDataAggregator>() ?? 
                Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(Config.Get("data-aggregator", "QuantConnect.Data.Common.CustomDataAggregator"));

            _subscriptionSymbols = new HashSet<Symbol>();
            _connectionGate = new RateGate(1, TimeSpan.FromSeconds(Config.GetInt("benzinga-connection-wait-timeout", 1)));
            _heartbeatInterval = TimeSpan.FromSeconds(Config.GetInt("benzinga-heartbeat-interval", 2));
            _responseTimeout = TimeSpan.FromSeconds(Config.GetInt("benzinga-response-timeout", 5));
            _overrideSubscriptionCheck = Config.GetBool("benzinga-override-subscription-check", true);

            _cancellationSource = new CancellationTokenSource();
            _tcpThread = new Thread(() => Read())
            {
                Name = $"BenzingaDQH",
                IsBackground = true
            };

            _tcpThread.Start();
        }

        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!CanSubscribe(dataConfig.Symbol))
            {
                return null;
            }

            lock (_subscriptionLock)
            {
                if (_subscriptionSymbols.Add(dataConfig.Symbol))
                {
                    Log.Trace($"BenzingaDataQueueHandler.Subscribe(): {dataConfig.Symbol}");
                }
            }
            return _dataAggregator.Add(dataConfig, newDataAvailableHandler);
        }

        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        /// <param name="dataConfig">Subscription config to be removed</param>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            lock (_subscriptionLock)
            {
                if (CanSubscribe(dataConfig.Symbol) && _subscriptionSymbols.Remove(dataConfig.Symbol))
                {
                    _dataAggregator.Remove(dataConfig);
                    Log.Trace($"BenzingaDataQueueHandler.Unsubscribe(): {dataConfig.Symbol}");
                }
            }
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
        }

        /// <summary>
        /// Returns whether the data provider is connected
        /// </summary>
        /// <returns>True if the data provider is connected</returns>
        public bool IsConnected => _connected;

        /// <summary>
        /// Determines whether we can add the Symbol to the Subscriptions collection
        /// </summary>
        /// <param name="symbol">Symbol to check</param>
        /// <returns>Boolean value indicating if we can add this Symbol to the current subscriptions</returns>
        private bool CanSubscribe(Symbol symbol)
        {
            // Allow overriding of subscriptions
            if (_overrideSubscriptionCheck)
            {
                return true;
            }

            // ignore unsupported security types
            if (symbol.ID.SecurityType != SecurityType.Base || symbol.ID.Symbol.Split('.').LastOrDefault() != typeof(BenzingaNews).Name)
            {
                return false;
            }

            // ignore universe symbols
            return !symbol.Value.Contains("-UNIVERSE-");
        }

        /// <summary>
        /// Main event loop for reading and connecting to the Benzinga TCP feed (https://docs.benzinga.io/benzinga/newsfeed-tcp-v1-1.html)
        /// </summary>
        /// <remarks>
        /// Ran in a separate thread
        /// </remarks>
        private void Read()
        {
            var liveJsonConverter = new BenzingaNewsLiveJsonConverter();

            while (!_cancellationSource.IsCancellationRequested && !_connected)
            {
                var authenticated = false;
                var messageReceived = false;
                var pingMessage = $"{Guid.NewGuid()}";
                var previous = string.Empty;
                var lastReply = DateTime.UtcNow;
                var lastPing = DateTime.MinValue;

                _connectionGate.WaitToProceed();

                try
                {
                    using (var client = Connect())
                    using (var socket = client.Client)
                    using (var stream = client.GetStream())
                    {
                        while (!_cancellationSource.IsCancellationRequested && _connected)
                        {
                            if (!client.Connected)
                            {
                                Log.Error("BenzingaDataQueueHandler.Read(): Connection closed", overrideMessageFloodProtection: true);
                                break;
                            }

                            var currentTime = DateTime.UtcNow;
                            var lastPingDelta = currentTime - lastPing;
                            var replyDelta = currentTime - lastReply;

                            if (!messageReceived && replyDelta > _responseTimeout)
                            {
                                Log.Error($"BenzingaDataQueueHandler.Read(): Connection timed out ({_responseTimeout.TotalSeconds} seconds)", overrideMessageFloodProtection: true);
                                break;
                            }
                            if (authenticated && lastPingDelta > _heartbeatInterval)
                            {
                                TryKeepAlive(socket, pingMessage);
                                lastPing = DateTime.UtcNow;
                            }
                            if (!stream.DataAvailable)
                            {
                                messageReceived = false;
                                continue;
                            }

                            messageReceived = true;
                            lastReply = DateTime.UtcNow;

                            foreach (var buf in ReadToEnd(stream))
                            {
                                var body = previous + Encoding.UTF8.GetString(buf);

                                if (!body.EndsWithInvariant(EOF))
                                {
                                    previous = body;
                                    continue;
                                }

                                body = body.TrimStart('\r', '\n');
                                previous = string.Empty;
                                var messageType = FromMessage(body);

                                switch (messageType)
                                {
                                    // Authentication negotiation phase
                                    case BenzingaMessageType.Ready:
                                        TryAuthenticate(socket);
                                        break;
                                    case BenzingaMessageType.ConnectionAck:
                                        Log.Trace($"BenzingaDataQueueHandler.Read(): Connected to Benzinga TCP feed", overrideMessageFloodProtection: true);
                                        authenticated = true;
                                        break;

                                    // Unknown unknowns
                                    case BenzingaMessageType.UnknownError:
                                        throw new IOException(body.Substring(0, body.IndexOf(':')));
                                    case BenzingaMessageType.UnknownCommand:
                                        Log.Error($"BenzingaDataQueueHandler.Read(): Server received unknown command");
                                        break;

                                    // Situations that require restarting the connection
                                    case BenzingaMessageType.Goodbye:
                                        Log.Trace($"BenzingaDataQueueHandler.Read(): Connection closed by Benzinga server (usually caused by not authenticating in time)", overrideMessageFloodProtection: true);
                                        _connected = false;
                                        break;
                                    case BenzingaMessageType.DuplicateConnection:
                                        Log.Error($"BenzingaDataQueueHandler.Read(): Duplicate session for user \"{_user}\" was started on {_host}:{_port}", overrideMessageFloodProtection: true);
                                        _connected = false;
                                        break;

                                    // Fatal errors from the server. Requires config changes and restarting of this class
                                    case BenzingaMessageType.BadKey:
                                        Log.Error($"BenzingaDataQueueHandler.Read(): FATAL: Failed to authenticate to Benzinga TCP feed");
                                        return;
                                    case BenzingaMessageType.BadKeyFormat:
                                        Log.Error($"BenzingaDataQueueHandler.Read(): FATAL: Failed to authenticate to Benzinga TCP feed because the key is in an incorrect format");
                                        return;

                                    // Messages received from the TCP stream
                                    case BenzingaMessageType.Stream:
                                        var newsData = DeconstructMessage<BenzingaNews>(messageType, body, liveJsonConverter);
                                        lock (_subscriptionLock)
                                        {
                                            var symbolsToEmit = _overrideSubscriptionCheck ?
                                                newsData.Symbols.Select(s => Symbol.CreateBase(typeof(BenzingaNews), s, Market.USA)) :
                                                _subscriptionSymbols.Where(s => s.HasUnderlying && newsData.Symbols.Contains(s.Underlying))
                                                    .ToList();

                                            foreach (var symbol in symbolsToEmit)
                                            {
                                                var clone = newsData.Clone();
                                                clone.Symbol = symbol;

                                                _dataAggregator.Update((BenzingaNews)clone);
                                            }
                                        }
                                        break;
                                    case BenzingaMessageType.Pong:
                                        var pong = DeconstructMessage<BenzingaPongMessage>(BenzingaMessageType.Pong, body);
                                        if (pingMessage != pong.Value)
                                        {
                                            Log.Error($"Expected {pingMessage} in pong reply, but got {pong.Value}", overrideMessageFloodProtection: true);
                                            _connected = false;
                                        }
                                        pingMessage = $"{Guid.NewGuid()}";
                                        break;

                                    default:
                                        throw new ArgumentException($"Encountered send-only message type in response: {body}");
                                }
                            }
                        }
                    }
                }
                catch (Exception err)
                {
                    Log.Error(err, overrideMessageFloodProtection: true);
                }

                if (!_cancellationSource.IsCancellationRequested)
                {
                    Log.Trace($"BenzingaDataQueueHandler.Read(): Disconnected from Benzinga TCP feed, attempting reconnect", overrideMessageFloodProtection: true);
                }

                _connected = false;
            }
        }

        /// <summary>
        /// Attempt to create a new <see cref="TcpClient"/> by establishing
        /// a connection to the TCP server.
        /// </summary>
        /// <returns>TCP client</returns>
        private TcpClient Connect()
        {
            var socket = new TcpClient();
            socket.Connect(_host, _port);

            _connected = socket.Connected;
            return socket;
        }

        /// <summary>
        /// Constructs and sends a new <see cref="BenzingaMessageType.Ping"/> message
        /// to the server
        /// </summary>
        /// <param name="socket">Socket to send the message through</param>
        /// <param name="pingValue">Value we want to include in the ping message</param>
        private void TryKeepAlive(Socket socket, string pingValue)
        {
            var messageType = ToMessageString(BenzingaMessageType.Ping);
            var message = new BenzingaPingMessage(pingValue).ConstructMessageBytes(messageType, EOF);
            SendMessage(socket, message);
        }

        /// <summary>
        /// Constructs and sends a new <see cref="BenzingaMessageType.Auth"/> message
        /// to the server to begin the authentication process.
        /// </summary>
        /// <param name="socket">Socket to send the message through</param>
        private void TryAuthenticate(Socket socket)
        {
            var messageType = ToMessageString(BenzingaMessageType.Auth);
            var message = new BenzingaAuthMessage(_user, _key).ConstructMessageBytes(messageType, EOF);
            SendMessage(socket, message);
        }

        /// <summary>
        /// Sends the supplied bytes via the supplied Socket
        /// </summary>
        /// <param name="socket">Socket ot send the message through</param>
        /// <param name="message">Message to send</param>
        private void SendMessage(Socket socket, byte[] message)
        {
            // Since the lifetime of SocketAsyncEventArgs is less than the subscriber's
            // lifetime, the event will be garbage collected without causing any memory leaks.
            var sendArgs = new SocketAsyncEventArgs();
            sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(SetConnectionStatus);
            sendArgs.SetBuffer(message, 0, message.Length);

            if (!socket.SendAsync(sendArgs))
            {
                SetConnectionStatus(this, sendArgs);
            }
        }

        /// <summary>
        /// Reads until the established Benzinga <see cref="EOF"/> or until
        /// we run out of data to read.
        /// </summary>
        /// <param name="stream">Stream to read data from</param>
        /// <returns>
        /// yields complete messages (with "=BZEOT\r\n" ending) or a partial
        /// message when the stream has no more data to return
        /// </returns>
        private IEnumerable<byte[]> ReadToEnd(NetworkStream stream)
        {
            var eof = Encoding.UTF8.GetBytes(EOF);
            var last = new Queue<byte>(eof.Length);

            var buf = new byte[4096];
            var parsing = true;

            while (!_cancellationSource.IsCancellationRequested && stream.DataAvailable)
            {
                var received = stream.Read(buf, 0, buf.Length);
                var offset = 0;

                for (var i = 0; i < received; i++)
                {
                    parsing = true;

                    // Once we reach the same number of bytes as the EOF
                    // string, let's push out the first byte inserted to make room
                    // for a new character.
                    if (last.Count == eof.Length)
                    {
                        last.Dequeue();
                    }

                    last.Enqueue(buf[i]);

                    if (last.SequenceEqual(eof))
                    {
                        yield return buf.Skip(offset).Take((i - offset) + 1).ToArray();
                        last.Clear();
                        parsing = false;
                        offset = i;
                    }
                }

                if (parsing)
                {
                    yield return buf.Skip(offset).Take(received - offset).ToArray();
                }
            }
        }

        /// <summary>
        /// Sets the connection status
        /// </summary>
        /// <param name="sender">Who called this method</param>
        /// <param name="e">Socket event</param>
        private void SetConnectionStatus(object sender, SocketAsyncEventArgs e)
        {
            _connected = e.SocketError == SocketError.Success;
        }

        /// <summary>
        /// Deconstructs a message received from Benzinga
        /// </summary>
        /// <typeparam name="T">Message to deserialize into</typeparam>
        /// <param name="messageType">Type of message according to Benzinga</param>
        /// <param name="response">The contents of the message</param>
        /// <param name="converter">Optional <see cref="JsonConverter"/> instance to use in deserialization</param>
        /// <returns>Deserialized message</returns>
        private T DeconstructMessage<T>(BenzingaMessageType messageType, string response, JsonConverter converter = null)
        {
            var messageStart = ToMessageString(messageType);
            var responseBody = response;

            if (responseBody.StartsWithInvariant(messageStart))
            {
                responseBody = responseBody.Substring(messageStart.Length);
            }
            if (responseBody.EndsWithInvariant(EOF))
            {
                responseBody = responseBody.Substring(0, responseBody.Length - EOF.Length);
            }

            return converter == null ?
                JsonConvert.DeserializeObject<T>(responseBody) :
                JsonConvert.DeserializeObject<T>(responseBody, converter);
        }

        /// <summary>
        /// Determines the <see cref="BenzingaMessageType"/> based off
        /// of the message's signature
        /// </summary>
        /// <param name="message">Message received from Benzinga</param>
        /// <returns>BenzingaMessageType</returns>
        private BenzingaMessageType FromMessage(string message)
        {
            if (message.StartsWithInvariant("UNKNOWN COMMAND"))
            {
                return BenzingaMessageType.UnknownCommand;
            }
            if (message.StartsWithInvariant("ERROR: "))
            {
                return BenzingaMessageType.UnknownError;
            }
            if (message.StartsWithInvariant("READY"))
            {
                return BenzingaMessageType.Ready;
            }
            if (message.StartsWithInvariant("CONNECTED"))
            {
                return BenzingaMessageType.ConnectionAck;
            }
            if (message.StartsWithInvariant("Goodbye"))
            {
                return BenzingaMessageType.Goodbye;
            }
            if (message.StartsWithInvariant("INVALID KEY FORMAT"))
            {
                return BenzingaMessageType.BadKey;
            }
            if (message.StartsWithInvariant("INVALID KEY"))
            {
                return BenzingaMessageType.BadKeyFormat;
            }
            if (message.StartsWithInvariant("DUPLICATE CONNECTION"))
            {
                return BenzingaMessageType.DuplicateConnection;
            }
            if (message.StartsWithInvariant("AUTH: "))
            {
                return BenzingaMessageType.Auth;
            }
            if (message.StartsWithInvariant("PING: "))
            {
                return BenzingaMessageType.Ping;
            }
            if (message.StartsWithInvariant("STREAM: "))
            {
                return BenzingaMessageType.Stream;
            }
            if (message.StartsWithInvariant("PONG: "))
            {
                return BenzingaMessageType.Pong;
            }

            throw new ArgumentException($"Invalid message encountered: {message}");
        }

        /// <summary>
        /// Converts the <see cref="BenzingaMessageType"/> into the string the
        /// server expects the message to be prepended with.
        /// </summary>
        /// <param name="message">Message type</param>
        /// <returns>
        /// string usable to identify a message or let the server know
        /// what message we're sending to it.
        /// </returns>
        private string ToMessageString(BenzingaMessageType message)
        {
            switch (message)
            {
                case BenzingaMessageType.UnknownError:
                    return "ERROR: ";
                case BenzingaMessageType.UnknownCommand:
                    return "UNKNOWN COMMAND";
                case BenzingaMessageType.Ready:
                    return "READY";
                case BenzingaMessageType.ConnectionAck:
                    return "CONNECTED";
                case BenzingaMessageType.Goodbye:
                    return "Goodbye";

                case BenzingaMessageType.BadKey:
                    return "INVALID KEY";
                case BenzingaMessageType.BadKeyFormat:
                    return "INVALID KEY FORMAT";
                case BenzingaMessageType.DuplicateConnection:
                    return "DUPLICATE CONNECTION";

                case BenzingaMessageType.Auth:
                    return "AUTH: ";
                case BenzingaMessageType.Ping:
                    return "PING: ";

                case BenzingaMessageType.Stream:
                    return "STREAM: ";
                case BenzingaMessageType.Pong:
                    return "PONG: ";

                default:
                    throw new ArgumentException("BenzingaMessageType match is not exhaustive");
            }
        }

        /// <summary>
        /// Shuts down the background thread and sets the
        /// cancellation token to a canceled state.
        /// </summary>
        public void Dispose()
        {
            _cancellationSource.Cancel();
            _tcpThread.Join(TimeSpan.FromSeconds(30));
        }
    }
}
