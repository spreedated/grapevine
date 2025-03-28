using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Grapevine.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void ParseFormUrlEncodedDataTest()
        {
            IHttpRequest s = Mock.Of<IHttpRequest>((m) =>
                                                        m.InputStream == Stream.Null &&
                                                        m.Url == new Uri("http://api.some.test/api/test?username=moqTest&password=insecure")
                                                    );

            IDictionary<string, string> r = null;

            r = s.ParseFormUrlEncodedData().Result;

            Assert.Equal(2, r.Count);
            Assert.True(r.ContainsKey("username"));
            Assert.True(r.ContainsKey("password"));
            Assert.Equal("moqTest", r["username"]);
            Assert.Equal("insecure", r["password"]);

            r = s.ParseFormUrlEncodedData(true).Result;

            Assert.Equal(2, r.Count);
            Assert.True(r.ContainsKey("username"));
            Assert.True(r.ContainsKey("password"));
            Assert.Equal("moqTest", r["username"]);
            Assert.Equal("insecure", r["password"]);

            using (MemoryStream ms = new(Encoding.UTF8.GetBytes("username=moqTest&password=insecure")))
            {
                s = Mock.Of<IHttpRequest>((m) =>
                                                m.InputStream == ms &&
                                                m.Url == new Uri("http://api.some.test/api/test?username=moqTest&password=insecure")
                                            );

                r = s.ParseFormUrlEncodedData().Result;

                Assert.Equal(2, r.Count);
                Assert.True(r.ContainsKey("username"));
                Assert.True(r.ContainsKey("password"));
                Assert.Equal("moqTest", r["username"]);
                Assert.Equal("insecure", r["password"]);
            }
        }

        [Fact]
        public void StringExtensionsStartsWithTest()
        {
            string s = "someTestString here";
            string[] searchStrings = new string[] { "hello", "som" };

            Assert.True(s.StartsWith(searchStrings));

            searchStrings = new string[] { "hello", "there", "from", "seattle", "here" };

            Assert.False(s.StartsWith(searchStrings));
        }
    }
}
