using System.Net;

namespace ClientTcpUdp;
class Program
{
    public static Client? _server;
    public static int _delay = 1000;

    public static int _port = 4000;
    public static string _channel = "default";
    public static int _retransmissions = 3;
    static async Task Main(string[] args)
    {
        try
        {
            //Parsing arguments
            if (args.Length == 1 && Array.IndexOf(args, "-h") != -1)
            {
                PrintHelp();
                return;
            }
            if (Array.IndexOf(args, "-t") == -1 || Array.IndexOf(args, "-s") == -1)
            {
                throw new ArgumentException("Missing required arguments");
            }

            if (Array.IndexOf(args, "-p") != -1)
                _port = int.Parse(args[Array.IndexOf(args, "-p") + 1]);
            if (Array.IndexOf(args, "-d") != -1)
                _delay = int.Parse(args[Array.IndexOf(args, "-d") + 1]);
            if (Array.IndexOf(args, "-r") != -1)
                _retransmissions = int.Parse(args[Array.IndexOf(args, "-r") + 1]);

            if (Array.IndexOf(args, "-c") != -1)
                _channel = args[Array.IndexOf(args, "-c") + 1];


            string transportProtocol = args[Array.IndexOf(args, "-t") + 1];
            string serverHostname = args[Array.IndexOf(args, "-s") + 1];

            IPAddress[] address;

            // Resolve server hostname
            if ((address = Dns.GetHostAddresses(serverHostname)) == null)
            {

                throw new ArgumentException($"Failed to resolve server hostname: {serverHostname}");
            }
            // string domain = "localhost";
            // IPAddress[] addresses = Dns.GetHostAddresses(domain);

            if (transportProtocol == "tcp")
            {
                _server = new TCP(address[0], _port, _channel);
            }
            else if (transportProtocol == "udp")
            {
                _server = new UDP(address[0], _port, _delay, _retransmissions, _channel);
            }
            else
            {
                throw new ArgumentException("Invalid transport protocol");
            }
            //Manage exit
            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true;
                await _server.DisconnectAndExit();
            };
            //Start client
            await _server.WritePackets();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");

        }
    }
    static void PrintHelp()
    {
        Console.WriteLine("CLI arguments:");
        Console.WriteLine("-t\tMANDATORY\ttcp/udp\t\tTransport protocol used for connection");
        Console.WriteLine("-s\tMANDATORY\tIP/hostname\tServer IP or hostname");
        Console.WriteLine("-p\t4567\t\tuint16\t\tServer port");
        Console.WriteLine("-d\t250\t\tuint16\t\tUDP CONFIRM timeout");
        Console.WriteLine("-r\t3\t\tuint8\t\tMax UDP retransmissions");
        Console.WriteLine("-h\t\t\t\t\tPrints program help output and exits");
    }
}

