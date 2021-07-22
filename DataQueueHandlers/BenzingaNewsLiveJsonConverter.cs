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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.DataSource;
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
            var channels = token["channels"].ToList(x => x.Value<string>());
            // Normalize the live DateTimes as they differ from the Newsfeed API's date formats
            var created = NormalizeUtcDateTime(token["published"].Value<string>());
            var updated = NormalizeUtcDateTime(token["updated"].Value<string>());

            token["created"] = created;
            token["updated"] = updated;
            // Empty list for the meanwhile since the two formats are incompatible
            token["channels"] = JToken.FromObject(new List<string>());
            // Normalize the single ticker format (e.g. ["SPY", "AAPL"]) into the Newsfeed format.
            token["stocks"] = JToken.FromObject(token["tickers"]?.ToList(x => x).Select(x =>
            {
                if (x?.Type == JTokenType.String)
                {
                    var newToken = new JObject();
                    newToken["name"] = x;

                    return newToken;
                }

                return x;
            }) ?? new List<JToken>());

            var instance = BenzingaNewsJsonConverter.DeserializeNews(token);
            instance.Categories = channels;
            instance.Author = token["authors"]?
                .ToList(x => x["name"] != null ? x["name"].Value<string>() : x?.Value<string>())?
                .FirstOrDefault();

            // Set the endtime at the end since that's whenever we finished parsing the message
            instance.EndTime = DateTime.UtcNow;

            return instance;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Normalizes live TCP dates to a DateTime object
        /// </summary>
        /// <param name="date">Raw date</param>
        /// <returns>DateTime</returns>
        /// <remarks>Example <paramref name="rawDate"> is: "Wed Mar  4 2020 19:54:18 GMT+0000 (UTC)"</remarks>
        public static DateTime NormalizeUtcDateTime(string rawDate)
        {
            rawDate = Regex.Replace(rawDate, @"\s+", " ");
            rawDate = rawDate.Substring(0, rawDate.IndexOf("GMT")).Trim();
            return Parse.DateTimeExact(rawDate, "ddd MMM d yyyy HH:mm:ss", DateTimeStyles.AdjustToUniversal);
        }
    }
}
