using System.Linq;

namespace Grapevine.Models
{
    public sealed class Prefix
    {
        public enum Protocols
        {
            Http = 0,
            Https
        }
        public string Domain { get; set; }
        public int Port { get; set; }
        public Protocols Protocol { get; set; }
        public static Prefix Parse(string input)
        {
            if (!input.Contains(":") || !input.Contains("/") || input.Count(x => x == '/') < 2 || !input.ToLower().StartsWith("http"))
            {
                return null;
            }

            return new()
            {
                Protocol = input.Substring(0, input.IndexOf(':')).EndsWith("s") ? Protocols.Https : Protocols.Http,
                Domain = input.Substring(input.IndexOf('/') + 2, input.LastIndexOf(':') - input.IndexOf('/') - 2),
                Port = int.Parse(input.Substring(input.LastIndexOf(':') + 1).TrimEnd('/'))
            };
        }

        public override string ToString()
        {
            return $"{(this.Protocol.ToString().ToLower())}://{this.Domain}:{this.Port}/";
        }
    }
}
