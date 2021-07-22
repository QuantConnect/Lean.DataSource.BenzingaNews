﻿/*
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
using Newtonsoft.Json;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.DataSource;

namespace QuantConnect.DataLibrary.Tests
{
    [TestFixture]
    public class BenzingaNewsJsonConverterTests
    {
        [Test]
        public void DeserializesCorrectly()
        {
            var content = @"{
                ""id"": 1,
                ""author"": ""Gerardo"",
                ""created"": ""2018-01-25T12:00:00Z"",
                ""updated"": ""2018-01-26T12:00:00Z"",
                ""title"": ""Unit Test Beats Expectations"",
                ""teaser"": ""The unit test beat reviewer's expectations, reporters say"",
                ""body"": ""<p>The unit test beat reviewer's expectations, reporters say - 'This is the best test I've ever seen' says Martin</p>"",
                ""channels"": [
                    {
                        ""name"": ""earnings""
                    }
                ],
                ""stocks"": [
                    {
                        ""name"": ""AAPL""
                    }
                ],
                ""tags"": [
                    {
                        ""name"": ""unit test""
                    },
                    {
                        ""name"": ""testing""
                    }
                ]
            }";

            // Put in a single line to avoid potential failure due to platform-specific behavior (\r\n vs. \n)
            var expectedSerialized = @"{""id"":1,""author"":""Gerardo"",""created"":""2018-01-25T12:00:00Z"",""updated"":""2018-01-26T12:00:00Z"",""title"":""Unit Test Beats Expectations"",""teaser"":""The unit test beat reviewer's expectations, reporters say"",""body"":"" The unit test beat reviewer's expectations, reporters say - 'This is the best test I've ever seen' says Martin "",""channels"":[{""name"":""earnings""}],""stocks"":[{""name"":""AAPL""}],""tags"":[{""name"":""unit test""},{""name"":""testing""}],""EndTime"":""2018-01-26T12:00:00Z"",""DataType"":0,""IsFillForward"":false,""Time"":""2018-01-26T12:00:00Z"",""Symbol"":{""Value"":""AAPL"",""ID"":""AAPL.BenzingaNews R735QTJ8XC9W"",""Permtick"":""AAPL""},""Value"":0.0,""Price"":0.0}";
            var expectedSymbol = new Symbol(
                SecurityIdentifier.GenerateEquity(
                    "AAPL",
                    QuantConnect.Market.USA,
                    true,
                    null,
                    new DateTime(2018, 1, 25)
                ),
                "AAPL"
            );
            var expectedBaseSymbol = new Symbol(
                SecurityIdentifier.GenerateBase(
                    typeof(BenzingaNews),
                    "AAPL",
                    QuantConnect.Market.USA,
                    mapSymbol: true,
                    date: new DateTime(2018, 1, 25)
                ),
                "AAPL"
            );

            var result = JsonConvert.DeserializeObject<BenzingaNews>(content, new BenzingaNewsJsonConverter(symbol: expectedBaseSymbol, liveMode: false));
            var serializedResult = JsonConvert.SerializeObject(result, Formatting.None, new BenzingaNewsJsonConverter(symbol: expectedBaseSymbol, liveMode: false));
            var resultFromSerialized = JsonConvert.DeserializeObject<BenzingaNews>(serializedResult, new BenzingaNewsJsonConverter(symbol: expectedBaseSymbol, liveMode: false));

            Assert.AreEqual(expectedSerialized, serializedResult);

            Assert.AreEqual(1, result.Id);
            Assert.AreEqual(
                Parse.DateTimeExact("2018-01-25T12:00:00Z", "yyyy-MM-ddTHH:mm:ssZ", DateTimeStyles.AdjustToUniversal),
                result.CreatedAt);
            Assert.AreEqual(
                Parse.DateTimeExact("2018-01-26T12:00:00Z", "yyyy-MM-ddTHH:mm:ssZ", DateTimeStyles.AdjustToUniversal),
                result.UpdatedAt);

            Assert.AreEqual(result.UpdatedAt, result.EndTime);
            Assert.AreEqual(result.UpdatedAt, result.Time);

            Assert.AreEqual("Gerardo", result.Author);
            Assert.AreEqual("Unit Test Beats Expectations", result.Title);
            Assert.AreEqual("The unit test beat reviewer's expectations, reporters say", result.Teaser);
            Assert.AreEqual(" The unit test beat reviewer's expectations, reporters say - 'This is the best test I've ever seen' says Martin ", result.Contents);

            Assert.AreEqual(new List<string> { "earnings" }, result.Categories);
            Assert.AreEqual(new List<string> { "unit test", "testing" }, result.Tags);
            Assert.AreEqual(new List<Symbol> { expectedSymbol }, result.Symbols);

            // Now begin comparing the resultFromSerialized and result instances
            Assert.AreEqual(result.Id, resultFromSerialized.Id);
            Assert.AreEqual(result.Author, resultFromSerialized.Author);
            Assert.AreEqual(result.CreatedAt, resultFromSerialized.CreatedAt);
            Assert.AreEqual(result.UpdatedAt, resultFromSerialized.UpdatedAt);
            Assert.AreEqual(result.Title, resultFromSerialized.Title);
            Assert.AreEqual(result.Teaser, resultFromSerialized.Teaser);
            Assert.AreEqual(result.Contents, resultFromSerialized.Contents);
            Assert.AreEqual(result.Categories, resultFromSerialized.Categories);
            Assert.AreEqual(result.Symbols, resultFromSerialized.Symbols);
            Assert.AreEqual(result.Tags, resultFromSerialized.Tags);
        }

        [Test]
        public void SerializeRoundTrip()
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

            var createdAt = new DateTime(2020, 3, 19, 10, 0, 0);
            var updatedAt = new DateTime(2020, 3, 19, 10, 15, 0);

            var item = new BenzingaNews
            {
                Id = 1,
                Symbol = Symbols.SPY,
                Title = "title",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
            };

            var serialized = JsonConvert.SerializeObject(item, settings);
            var deserialized = JsonConvert.DeserializeObject<BenzingaNews>(serialized, settings);

            Assert.AreEqual(1, deserialized.Id);
            Assert.AreEqual(Symbols.SPY, deserialized.Symbol);
            Assert.AreEqual("title", deserialized.Title);
            Assert.AreEqual(createdAt, deserialized.CreatedAt);
            Assert.AreEqual(updatedAt, deserialized.UpdatedAt);
            Assert.AreEqual(updatedAt, deserialized.Time);
            Assert.AreEqual(updatedAt, deserialized.EndTime);
        }

        [Test]
        public void SerializeWithConverterNullValuesIgnored()
        {
            var date = new DateTime(2020, 3, 19);

            var converter = new BenzingaNewsJsonConverter();
            var instance = new BenzingaNews
            {
                Author = "",
                Categories = new List<string>(),
                Contents = "",
                CreatedAt = date,
                Id = default(int),
                Symbol = null,
                Symbols = new List<Symbol>
                {
                    Symbol.Create("AAPL", SecurityType.Equity, QuantConnect.Market.USA),
                    Symbol.Create("SPY", SecurityType.Equity, QuantConnect.Market.USA)
                },
                Tags = new List<string>
                {
                    "Politics"
                },
                Teaser = "",
                Time = date,
                Title = "",
                UpdatedAt = date,
            };

            var expectedSerialized = @"{""id"":0,""author"":"""",""created"":""2020-03-19T00:00:00Z"",""updated"":""2020-03-19T00:00:00Z"",""title"":"""",""teaser"":"""",""body"":"""",""channels"":[],""stocks"":[{""name"":""AAPL""},{""name"":""SPY""}],""tags"":[{""name"":""Politics""}],""EndTime"":""2020-03-19T00:00:00Z"",""DataType"":0,""IsFillForward"":false,""Time"":""2020-03-19T00:00:00Z"",""Value"":0.0,""Price"":0.0}";
            var actualSerialized = JsonConvert.SerializeObject(instance, new JsonSerializerSettings
            {
                Converters = new[] { converter },
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Formatting.None
            });

            Assert.AreEqual(expectedSerialized, actualSerialized);

            var instanceRoundTripActual = JsonConvert.DeserializeObject<BenzingaNews>(actualSerialized, converter);

            Assert.AreEqual(instance.Author, instanceRoundTripActual.Author);
            Assert.IsTrue(instance.Categories.SequenceEqual(instanceRoundTripActual.Categories));
            Assert.AreEqual(instance.Contents, instanceRoundTripActual.Contents);
            Assert.AreEqual(instance.CreatedAt, instanceRoundTripActual.CreatedAt);
            Assert.AreEqual(instance.Id, instanceRoundTripActual.Id);
            Assert.AreEqual(instance.Symbol, instanceRoundTripActual.Symbol);
            Assert.IsTrue(instance.Symbols.SequenceEqual(instanceRoundTripActual.Symbols));
            Assert.IsTrue(instance.Tags.SequenceEqual(instanceRoundTripActual.Tags));
            Assert.AreEqual(instance.Teaser, instanceRoundTripActual.Teaser);
            Assert.AreEqual(instance.Time, instanceRoundTripActual.Time);
            Assert.AreEqual(instance.Title, instanceRoundTripActual.Title);
            Assert.AreEqual(instance.UpdatedAt, instanceRoundTripActual.UpdatedAt);
        }

        [Test]
        public void ReaderDeserializesInUtc()
        {
            var date = new DateTime(2020, 3, 19);
            var spy = Symbol.Create("SPY", SecurityType.Equity, QuantConnect.Market.USA);

            var converter = new BenzingaNewsJsonConverter();
            var instance = new BenzingaNews
            {
                Author = "",
                Categories = new List<string>(),
                Contents = "",
                CreatedAt = date,
                Id = default(int),
                Symbol = spy,
                Symbols = new List<Symbol>
                {
                    Symbol.Create("AAPL", SecurityType.Equity, QuantConnect.Market.USA),
                    Symbol.Create("SPY", SecurityType.Equity, QuantConnect.Market.USA)
                },
                Tags = new List<string>
                {
                    "Politics"
                },
                Teaser = "",
                Time = date,
                Title = "",
                UpdatedAt = date,
            };

            var serialized = JsonConvert.SerializeObject(instance, new JsonSerializerSettings
            {
                Converters = new[] { converter },
                Formatting = Formatting.None,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            });

            var config = new SubscriptionDataConfig(
                typeof(BenzingaNews),
                spy,
                Resolution.Minute,
                TimeZones.Utc,
                TimeZones.Utc,
                true,
                false,
                false);

            var news = new BenzingaNews();
            var instanceRoundTripActual = (BenzingaNews)news.Reader(config, serialized, default(DateTime), false);

            Assert.AreEqual(instance.Author, instanceRoundTripActual.Author);
            Assert.IsTrue(instance.Categories.SequenceEqual(instanceRoundTripActual.Categories));
            Assert.AreEqual(instance.Contents, instanceRoundTripActual.Contents);
            Assert.AreEqual(instance.CreatedAt, instanceRoundTripActual.CreatedAt);
            Assert.AreEqual(instance.Id, instanceRoundTripActual.Id);
            Assert.AreEqual(instance.Symbol, instanceRoundTripActual.Symbol);
            Assert.IsTrue(instance.Symbols.SequenceEqual(instanceRoundTripActual.Symbols));
            Assert.IsTrue(instance.Tags.SequenceEqual(instanceRoundTripActual.Tags));
            Assert.AreEqual(instance.Teaser, instanceRoundTripActual.Teaser);
            Assert.AreEqual(instance.Time, instanceRoundTripActual.Time);
            Assert.AreEqual(instance.Title, instanceRoundTripActual.Title);
            Assert.AreEqual(instance.UpdatedAt, instanceRoundTripActual.UpdatedAt);

        }
    }
}
