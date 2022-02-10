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
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.DataSource.DataQueueHandlers
{
    public class BenzingaNewsLiveJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            token = token["content"];

            if (token == null)
            {
                return null;
            }

            var instance = new BenzingaNews
            {
                Id = token.Value<int>("id"),
                Symbols = new List<Symbol>(),
                Teaser = token["teaser"]?.Value<string>() ?? string.Empty,
                Title = token.Value<string>("title"),
                Categories = token["channels"]?.ToList(x => x.Value<string>()) ?? new List<string>(),
                CreatedAt = token.Value<DateTime>("created_at").ToUniversalTime(),
                UpdatedAt = token.Value<DateTime>("updated_at").ToUniversalTime(),
                Author = token["authors"]?.ToList(x => x.Value<string>())?.FirstOrDefault(),
                // Strip all HTML tags from the article, then convert HTML entities to their string representation
                // e.g. "<html><p>Apple&#39;s Earnings</p></html>" would become "Apple's Earnings"
                Contents = WebUtility.HtmlDecode(Regex.Replace(token.Value<string>("body"), @"<[^>]*>", " ")),
                // Set the endtime at the end since that's whenever we finished parsing the message
                EndTime = DateTime.UtcNow
            };

            if (token["securities"] != null)
            {
                foreach (var ticker in JArray.Parse(token["securities"].ToString()))
                {
                    // Tickers with dots in them like BRK.A and BRK.B appear as BRK-A and BRK-B in Benzinga data.
                    // They can also appear as BRK/B or BRK/A in some instances.
                    var symbolTicker = ticker.Value<string>("symbol").Trim().Replace('-', '.').Replace('/', '.');

                    // Tickers can be empty/null in Benzinga API responses.
                    // Verified by observing and processing empty ticker
                    if (string.IsNullOrWhiteSpace(symbolTicker))
                    {
                        Log.Error($"BenzingaNewsJsonConverter.DeserializeNews(): Empty ticker was found in article with ID: {instance.Id}");
                        continue;
                    }
                    instance.Symbols.Add(new Symbol(
                        SecurityIdentifier.GenerateEquity(symbolTicker, Market.USA, mapSymbol: true, mappingResolveDate: instance.CreatedAt),
                        symbolTicker
                    ));
                }
            }

            return instance;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
