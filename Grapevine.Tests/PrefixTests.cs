using Grapevine.Models;
using NUnit.Framework;
using Shouldly;

namespace Grapevine.Tests
{
    public class PrefixTests
    {
        [Test]
        public void ParseAndAddPrefixes()
        {
            Prefix p = Prefix.Parse("http://localhost:1234");

            p.Port.ShouldBe(1234);
            p.Domain.ShouldBe("localhost");
            p.Protocol.ShouldBe(Prefix.Protocols.Http);
            p.ToString().ShouldBe("http://localhost:1234/");

            p = Prefix.Parse("https://someone:7892/");

            p.Port.ShouldBe(7892);
            p.Domain.ShouldBe("someone");
            p.Protocol.ShouldBe(Prefix.Protocols.Https);
            p.ToString().ShouldBe("https://someone:7892/");

            p = new()
            {
                Domain = "localhost",
                Port = 1234,
                Protocol = Prefix.Protocols.Http
            };

            p.ToString().ShouldBe("http://localhost:1234/");

            p = new();

            p.ToString().ShouldBe("http://:0/");
        }

    }
}