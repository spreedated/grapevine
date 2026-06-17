using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Grapevine.Tests
{
    public class ExtensionsTests
    {
        [Test]
        public void ParseFormUrlEncodedDataTest()
        {
            IHttpRequest s = Mock.Of<IHttpRequest>((m) =>
                                                        m.InputStream == Stream.Null &&
                                                        m.Url == new Uri("http://api.some.test/api/test?username=moqTest&password=insecure")
                                                    );

            IDictionary<string, string> r = null;

            r = s.ParseFormUrlEncodedData().Result;

            Assert.That(r.Count, Is.EqualTo(2));
            Assert.True(r.ContainsKey("username"));
            Assert.True(r.ContainsKey("password"));
            Assert.That(r["username"], Is.EqualTo("moqTest"));
            Assert.That(r["password"], Is.EqualTo("insecure"));

            r = s.ParseFormUrlEncodedData(true).Result;

            Assert.That(r.Count, Is.EqualTo(2));
            Assert.True(r.ContainsKey("username"));
            Assert.True(r.ContainsKey("password"));
            Assert.That(r["username"], Is.EqualTo("moqTest"));
            Assert.That(r["password"], Is.EqualTo("insecure"));

            using (MemoryStream ms = new(Encoding.UTF8.GetBytes("username=moqTest&password=insecure")))
            {
                s = Mock.Of<IHttpRequest>((m) =>
                                                m.InputStream == ms &&
                                                m.Url == new Uri("http://api.some.test/api/test?username=moqTest&password=insecure")
                                            );

                r = s.ParseFormUrlEncodedData().Result;

                Assert.That(r.Count, Is.EqualTo(2));
                Assert.True(r.ContainsKey("username"));
                Assert.True(r.ContainsKey("password"));
                Assert.That(r["username"], Is.EqualTo("moqTest"));
                Assert.That(r["password"], Is.EqualTo("insecure"));
            }
        }

        [Test]
        public void StringExtensionsStartsWithTest()
        {
            string s = "someTestString here";
            string[] searchStrings = ["hello", "som"];

            Assert.True(s.StartsWith(searchStrings));

            searchStrings = ["hello", "there", "from", "seattle", "here"];

            Assert.False(s.StartsWith(searchStrings));
        }
    }
}
