using Grapevine;
using Grapevine.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SamepleServer
{
    internal static class Program
    {
        private static IRestServer restServer;
        static void Main(string[] args)
        {
            restServer = RestServerBuilder.BuildDefaultServer(Prefix.Parse("http://localhost:1234"));

            Task.Factory.StartNew(() =>
            {
                restServer.Start();
            });

            while (true)
            {
                Thread.Sleep(50);
            }
        }
    }
}