using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ClientTcpUdp;

class TCP : Client
{
    public TcpClient TcpCli { get; set; }
    IPAddress _address;
    int _port;
    StreamReader? _reader = null;
    StreamWriter? _writer = null;
    private readonly object _lock = new object();


    public TCP(IPAddress address, int port, string channelID)
    {
        _address = address;
        _port = port;
        Username = "";
        DisplayName = "";
        Secret = "";
        ChannelID = channelID;
        NextChannel = "";
        TcpCli = new TcpClient();
        CurrentState = States.Start;
    }
    //Function manages sending data to the server   
    public async Task SendToServer(string message)
    {
        if (_writer != null)
        {
            await _writer.WriteAsync(message);
            await _writer.FlushAsync();
        }
    }
    //Function reads data from the server
    public async Task<Message?> ReadFromServer()
    {
        try
        {
            var reply = new StringBuilder();
            char[] buffer = new char[1];
            while (true)//loop that reads user input until \r\n
            {
                if (TcpCli == null || _reader == null || _writer == null)
                {
                    return null;
                }
                await _reader.ReadAsync(buffer, 0, 1);
                reply.Append(buffer[0]);
                if (buffer[0] == '\n' && reply.Length >= 2 && reply[reply.Length - 2] == '\r')
                {
                    break;
                }
            }

            var message = new Message();
            return message.ParseMessage(reply.ToString()) ? message : null;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"ERR: Unable to receive message. {e.Message}");
            CleanResourcesAndExit();
        }
        return null;
    }
    //Client connects to the server after /auth command
    public override async Task ConnectToServer(string username, string secret, string displayName)
    {
        if (TcpCli.Connected)//when /auth is called again
        {
            Console.Error.WriteLine("ERR: Already connected to the server");
            return;
        }
        try
        {
            var endPoint = new IPEndPoint(_address, _port);
            await TcpCli.ConnectAsync(endPoint);

            var stream = TcpCli.GetStream();
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream);

            Username = username;
            DisplayName = displayName;
            Secret = secret;

            var send = Message.BuildAUTH(Username, DisplayName, Secret);
            if (send == "---")
            {
                Console.Error.WriteLine("ERR: Unable to send AUTH");
                CleanResourcesAndExit();
            }
            await SendToServer(send);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"ERR: Unable to send AUTH. {e.Message}");
            CleanResourcesAndExit();
        }
        await ReadMessages();
        //send message and wait for confirmation
        if (CurrentState == States.Open)
        {
            _ = Task.Run(() => ReadPackets());
        }
    }

    // Client joins the channel after /join command
    public async Task JoinChannel(string NewChannelID)
    {
        if (CurrentState == States.End || CurrentState == States.Error)
        {
            return;
        }
        if (CurrentState != States.Auth && CurrentState != States.Open)
        {
            Console.Error.WriteLine("ERR: You need to authenticate first");
            return;
        }

        NextChannel = NewChannelID;
        //send JOIN message
        var send = Message.BuildJOIN(NewChannelID, DisplayName);
        if (send == "---")
        {
            Console.Error.WriteLine("ERR: Unable to send JOIN");
            CleanResourcesAndExit();
        }
        await SendToServer(send);
    }
    //Read messages from the server
    public override async Task ReadMessages()
    {
        var parsed = await ReadFromServer();
        if (parsed == null)
        {
            if (CurrentState != States.Error || CurrentState != States.End)
            {//message was invalid
                CurrentState = States.Error;
            }
            return;
        }
        switch (parsed.MessageType)
        {
            case MsgTypes.ERR:
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }
                Console.Error.WriteLine($"ERR FROM {parsed.DisplayName}: {parsed.MessageContent}");
                CurrentState = States.End;//send BYE
                await DisconnectAndExit();
                break;
            case MsgTypes.REPLYnok://loop
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }
                if (CurrentState == States.Start)
                {//authentication failed, user needs to re-enter credentials
                    Console.Error.WriteLine($"Failure: {parsed.MessageContent}");
                    TcpCli.Close();

                    _reader!.Close();
                    _writer!.Close();

                    Username = "";
                    DisplayName = "";
                    Secret = "";
                    Console.Error.WriteLine("Re-enter credentials to authenticate");
                    break;
                }
                Console.Error.WriteLine($"Failure: {parsed.MessageContent}");
                if (ChannelID != "")
                {
                    _joined = true;//rejoined previous channel
                }
                break;
            case MsgTypes.REPLYok:
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }
                if (CurrentState == States.Start)
                {//authentication successful
                    Console.Error.WriteLine($"Success: {parsed.MessageContent}");
                    CurrentState = States.Open;
                    break;
                }
                Console.Error.WriteLine($"Success: {parsed.MessageContent}");
                if (NextChannel != "")
                {//joined new channel
                    ChannelID = NextChannel;
                }
                CurrentState = States.Open;
                _joined = true;
                break;
            case MsgTypes.MSG://message from the server
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }
                else if (_joined && CurrentState == States.Open)
                {
                    Console.WriteLine($"{parsed.DisplayName}: {parsed.MessageContent}");
                }
                else if (!_joined)
                {//for input from file 
                    Task.Delay(500).Wait();
                }
                else if (!_joined)
                {
                    Console.Error.WriteLine($"ERR: You need to be in a channel to receive messages");
                }
                else
                {
                    Console.Error.WriteLine($"ERR: You need to be in OPEN state");
                }
                break;
            case MsgTypes.BYE://server disconnected
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }
                CurrentState = States.End;
                CleanResourcesAndExit();//Don not send BYE
                break;
            default:
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }
                CurrentState = States.Error;
                break;
        }
    }

    //Function helps to orient in states
    public override async Task ReadPackets()
    {
        while (CurrentState != States.End && TcpCli.Connected)
        {
            switch (CurrentState)
            {
                case States.Start:
                case States.Auth:
                case States.Open:
                    await ReadMessages();
                    break;
                case States.Error:
                    await SendToServer(Message.BuildERR(DisplayName, "Invalid message"));
                    CurrentState = States.End;//send BYE
                    await DisconnectAndExit();
                    break;
                default:
                    break;
            }
        }
    }

    //read user input from stdin
    public override async Task WritePackets()
    {
        while (true)
        {
            var userInput = Console.ReadLine();
            if (string.IsNullOrEmpty(userInput))
            {
                // Ctrl+D, Ctrl+C, EOF, empty line
                CurrentState = States.End;//send BYE
                await DisconnectAndExit();
                return;
            }
            await ProcessInput(userInput);
            //for input from file
            Task.Delay(500).Wait();
        }
    }
    public override async Task ProcessInput(string userInput)
    {
        if (userInput.StartsWith("/"))
        {
            var commandParts = userInput.Substring(1).Split(' ');
            var command = commandParts[0];
            var parameters = commandParts[1..];

            switch (command)
            {
                case "auth": // /auth username secret displayName
                    if (parameters.Length != 3)
                    {
                        Console.Error.WriteLine("ERR: Invalid number of parameters for /auth command");
                        return;
                    }
                    await ConnectToServer(parameters[0], parameters[1], parameters[2]);
                    break;
                case "join": // /join channelID
                    if (parameters.Length != 1)
                    {
                        Console.Error.WriteLine("ERR: Invalid number of parameters for /join command");
                        return;
                    }
                    _joined = false;
                    await JoinChannel(parameters[0]);
                    break;
                case "rename": // /rename displayName
                    RenameCommand(parameters);
                    break;
                case "help": // /help
                    PrintHelp();
                    break;
                default:
                    Console.Error.WriteLine($"ERR: Unknown command '{command}'");
                    break;
            }
        }
        else
        {
            if (CurrentState == States.End || CurrentState == States.Error)
            {
                return;
            }
            else if (!_joined)
            {//for input from file
                Task.Delay(500).Wait();
            }
            else if (!_joined)
            {
                Console.Error.WriteLine($"ERR: You need to be in a channel to receive messages");
            }
            if (CurrentState == States.Open)
            {
                var send = Message.BuildMSG(DisplayName, userInput);
                if (send == "---")
                {
                    Console.Error.WriteLine("ERR: Unable to send MSG");
                    CleanResourcesAndExit();
                }
                await SendToServer(send);
            }
            else
            {
                Console.Error.WriteLine("ERR: You need to authenticate first");
            }
        }
    }
    // Function sends BYE message to the server and exits
    public override async Task DisconnectAndExit()
    {
        if (TcpCli.Connected)
        {
            await SendToServer(Message.BuildBYE());
        }
        CleanResourcesAndExit();
    }
    //resource cleanup
    public void CleanResourcesAndExit()
    {
        try
        {
            if (TcpCli != null && TcpCli.Connected)
            {
                TcpCli.Close();
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error during resource cleanup: {e.Message}");
        }
        finally
        {
            Environment.Exit(0);
        }
    }
}