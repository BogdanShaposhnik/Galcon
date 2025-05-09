using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string ipAddress = "127.0.0.1";
        int port = 7777;

        var server = new GameServer(ipAddress, port);
        await server.StartAsync();
    }
}
