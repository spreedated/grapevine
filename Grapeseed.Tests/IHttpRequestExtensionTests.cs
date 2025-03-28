using Grapevine;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Grapeseed.Tests
{
    [TestFixture]
    public class IHttpRequestExtensionTests
    {
        [SetUp]
        public void SetUp()
        {

        }

        [Test]
        [TestCase(new string[0], new string[0])]
        [TestCase(new string[] { "value", "name" }, new string[] { "7178722227", "foobar" })]
        [TestCase(new string[] { "value" }, new string[] { "7178722227" })]
        public void ParseFormUrlEncodedDataSuccessTests(string[] keys, string[] values)
        {
            Mock<IHttpRequest> request = new();
            request.SetupGet(x => x.InputStream).Returns(Stream.Null);

            StringBuilder s = new();

            if (keys.Length > 0)
            {
                s.Append('?');
            }

            for (int i = 0; i < keys.Length; i++)
            {
                s.Append(keys[i]);
                s.Append('=');
                s.Append(values[i]);
                s.Append('&');
            }

            request.SetupGet(x => x.Url).Returns(new Uri($"http://127.0.0.1:54189/v1/rfid{s.ToString().TrimEnd('&')}"));

            IDictionary<string, string> expected = null;

            Assert.DoesNotThrowAsync(async () => { expected = await request.Object.ParseFormUrlEncodedData(); });
            Assert.That(expected, Has.Count.EqualTo(keys.Length));

            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(expected[keys[i]], Is.EqualTo(values[i]));
            }
        }

        [TearDown]
        public void TearDown()
        {

        }
    }
}
