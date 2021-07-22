

using NUnit.Framework;
using QuantConnect.DataQueueHandlers;
using System;

namespace QuantConnect.DataLibrary.Tests
{
    [TestFixture]
    public class BenzingaNewsLiveJsonConverterTests
    {
        [TestCase("Wed Mar 4 2020 19:54:18 GMT+0000 (UTC)")]
        [TestCase("Wed Mar   4 2020 19:54:18  GMT+0000 (UTC)")]
        [TestCase("Wed Mar    4 2020 19:54:18  GMT+0000 (UTC)")]
        [TestCase("Wed Mar     4 2020 19:54:18 GMT+0000  (UTC)")]
        [TestCase("Wed Mar      4 2020 19:54:18 GMT+0000  (UTC)")]
        [TestCase("Wed Mar       4 2020 19:54:18  GMT+0000 (UTC)")]
        public void NormalizeBadDateStringToUtc(string time)
        {
            var expected = new DateTime(2020, 3, 4, 19, 54, 18);
            expected = DateTime.SpecifyKind(expected, DateTimeKind.Utc);

            var actual = BenzingaNewsLiveJsonConverter.NormalizeUtcDateTime(time);

            Assert.AreEqual(expected, actual);
        }
    }
}
