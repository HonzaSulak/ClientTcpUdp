using System.Net;
using System.Net.Sockets;

namespace ClientTcpUdp;
class UDP : Client
{
    public UdpClient UdpCli { get; set; }
    List<UDPMessage>? _messages;//sent by client
    List<UDPMessage> _received;//received from server
    private readonly object _lock = new object();//mutex

    IPAddress _address;
    int _port;
    int _dynamicPort;//given by server
    int _delay;
    int _retransmissions;//number of repetitions
    public UDP(IPAddress address, int port, int delay, int retransmissions, string channelID)
    {
        _address = address;
        _port = port;
        _dynamicPort = 0;
        _delay = delay;
        _retransmissions = retransmissions + 1;//+1 for the first message
        Username = "";
        DisplayName = "";
        Secret = "";
        ChannelID = channelID;
        NextChannel = "";
        UdpCli = new UdpClient();

        _received = new List<UDPMessage>();
        _messages = new List<UDPMessage>();
        CurrentState = States.Start;//initial state
    }
    //Function manages sending data to the server   
    public async Task SendToServer(byte[] data)
    {
        var endPoint = new IPEndPoint(_address, _dynamicPort);

        await UdpCli.SendAsync(data, data.Length, endPoint);
    }
    //Special function for UDP messages
    //Function sends message to the server and waits for confirmation
    public async Task SendFunc(UDPMessage message, byte[] function)
    {
        try
        {
            if (_messages != null)
            {
                lock (_lock)
                {
                    _messages.Add(message);
                }
                var send = function;
                UDPMessage? find;
                int count = 0;
                do
                {
                    await SendToServer(send);
                    await Task.Delay(_delay);//wait for confirmation
                    //search for message in the list
                    find = _messages.FirstOrDefault(x => x.MessageID == message.MessageID);
                    count++;
                    if (count == _retransmissions && !find!._confirmed)//error and exit
                    {
                        Console.Error.WriteLine("ERR: No confirmation received");
                        CleanResourcesAndExit();
                    }
                } while (!find!._confirmed && count < _retransmissions);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"ERR: Unable to send message. {e.Message}");
            CleanResourcesAndExit();//error and exit
        }
    }
    //Function reads data from the server
    public async Task<UDPMessage?> ReadFromServer()
    {
        var result = await UdpCli.ReceiveAsync();
        //assign dynamic port
        if (result.RemoteEndPoint.Port != _port && _dynamicPort == 0)
        {
            _dynamicPort = result.RemoteEndPoint.Port;
            _port = _dynamicPort;
        }
        var data = result.Buffer;
        var message = new UDPMessage();//no MessageID
        //if parsing was successful return message
        if(message.ParseMessage(data))
        {
            return message;
        }
        return null;
    }
    //Client connects to the server after /auth command
    public override async Task ConnectToServer(string username, string secret, string displayName)
    {
        Username = username;
        DisplayName = displayName;
        Secret = secret;
        //send AUTH message
        UDPMessage message = new UDPMessage((byte)UDPMsgType.AUTH, Username, DisplayName, Secret);
        if (_messages != null)
        {
            lock (_lock)
            {
                _messages.Add(message);
            }
            var send = message.BuildAUTH();
            var endPoint = new IPEndPoint(_address, _port);
            UDPMessage? find;
            int count = 0;
            //send message and wait for confirmation
            _ = Task.Run(() => ReadPackets());
            try
            {

                do
                {
                    if (UdpCli == null)
                    {
                        return;
                    }
                    await UdpCli.SendAsync(send, send.Length, endPoint);//inserted port
                    await Task.Delay(_delay);//wait for confirmation
                    find = _messages.FirstOrDefault(x => x.MessageID == message.MessageID);
                    count++;
                    if (count == _retransmissions && !find!._confirmed)//error and exit
                    {
                        Console.Error.WriteLine("ERR: No confirmation received");
                        CleanResourcesAndExit();
                    }
                } while (!find!._confirmed && count < _retransmissions);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"ERR: Unable to send message. {e.Message}");
                CleanResourcesAndExit();
            }
        }
    }
    // Client joins the channel after /join command
    public void JoinChannel(string newChannelID)
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
        ChannelID = newChannelID;
        UDPMessage message = new UDPMessage((byte)UDPMsgType.JOIN, DisplayName, ChannelID);
        //send JOIN message
        _ = Task.Run(() => SendFunc(message, message.BuildJOIN()));

    }
    //Client sends error message to the server
    public void ErrorToServer(string messageContents)
    {
        UDPMessage message = new UDPMessage((byte)UDPMsgType.ERR, messageContents);
        message.DisplayName = DisplayName;
        //send ERR message
        _ = Task.Run(() => SendFunc(message, message.BuildERR()));
    }
    //Received confirmation from the server
    public void HandleConfirm(UDPMessage message)
    {
        lock (_lock)
        {
            if (_messages != null)
            {
                var find = _messages.FirstOrDefault(x => x.MessageID == message.Ref_MessageID);
                if (find != null)
                {
                    //if message was confirmed, stop sending another messages
                    find._confirmed = true;
                }
            }
        }
    }
    //Send confirmation to the server
    public async void SendConfirm(UDPMessage message)
    {
        UDPMessage confirm = new UDPMessage();//no MessageID
        confirm.Ref_MessageID = message.MessageID;
        //send CONFIRM message
        var send = confirm.BuildCONFIRM();
        var endPoint = new IPEndPoint(_address, _dynamicPort);

        await UdpCli.SendAsync(send, send.Length, endPoint);
    }
    //Read messages from the server
    public override async Task ReadMessages()
    {
        //process received messages
        var parsed = await ReadFromServer();

        if (parsed == null)
        {
            if (CurrentState != States.Error || CurrentState != States.End)
            {//message was invalid
                CurrentState = States.Error;
            }
            return;
        }
        UDPMessage? find;
        switch (parsed.Type)
        {
            case (byte)UDPMsgType.CONFIRM:
                HandleConfirm(parsed);//Auth confirmed
                break;
            case (byte)UDPMsgType.ERR:
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }//if message was already processed, skip
                find = _received.FirstOrDefault(x => x.MessageID == parsed.MessageID);
                if (find == null)
                {
                    lock (_lock)
                    {
                        _received.Add(parsed);
                    }
                    Console.Error.WriteLine($"ERR FROM {parsed.DisplayName}: {parsed.MessageContents}");
                    SendConfirm(parsed);
                    CurrentState = States.End;
                    await DisconnectAndExit();
                    break;

                }
                SendConfirm(parsed);
                break;
            case (byte)UDPMsgType.REPLY:
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }
                find = _received.FirstOrDefault(x => x.MessageID == parsed.MessageID);
                if (find == null)
                {
                    lock (_lock)
                    {
                        _received.Add(parsed);
                    }
                    if (parsed.Result == 0)//NOK
                    {
                        if (CurrentState == States.Start)
                        {//auth failed
                            Console.Error.WriteLine($"Failure: {parsed.MessageContents}");
                            Username = "";
                            DisplayName = "";
                            Secret = "";
                            Console.Error.WriteLine("Reenter credentials to authenticate");
                        }
                        else
                        {//join failed
                            Console.Error.WriteLine($"Failure: {parsed.MessageContents}");
                            if (ChannelID != "")
                            {
                                _joined = true;//rejoined previous channel
                            }
                        }
                    }
                    else if (parsed.Result == 1)//OK
                    {//auth or join success
                        Console.Error.WriteLine($"Success: {parsed.MessageContents}");
                        if (NextChannel != "")//joined new channel
                        {
                            ChannelID = NextChannel;
                        }
                        CurrentState = States.Open;
                        _joined = true;
                    }
                    else
                    {
                        CurrentState = States.Error;
                    }
                }
                SendConfirm(parsed);
                break;
            case (byte)UDPMsgType.MSG:
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }
                find = _received.FirstOrDefault(x => x.MessageID == parsed.MessageID);
                if (find == null)
                {
                    lock (_lock)
                    {
                        _received.Add(parsed);
                    }
                    //if client is in OPEN state and joined channel, print message
                    if (_joined && CurrentState == States.Open)
                    {
                        Console.WriteLine($"{parsed.DisplayName}: {parsed.MessageContents}");
                    }
                    else if (!_joined)
                    {//for folder inputs
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
                }

                SendConfirm(parsed);
                break;
            case (byte)UDPMsgType.BYE://server disconnected
                if (CurrentState == States.End || CurrentState == States.Error)
                {
                    break;
                }
                find = _received.FirstOrDefault(x => x.MessageID == parsed.MessageID);
                if (find == null)
                {
                    lock (_lock)
                    {
                        _received.Add(parsed);
                    }
                    SendConfirm(parsed);
                    CurrentState = States.End;
                    CleanResourcesAndExit();
                    break;
                }
                SendConfirm(parsed);
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
        while (CurrentState != States.End)
        {
            switch (CurrentState)
            {
                case States.Start:
                case States.Auth:
                case States.Open:
                    await ReadMessages();
                    break;
                case States.Error:
                    ErrorToServer("Invalid message");
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
                CurrentState = States.End;
                await DisconnectAndExit();
                return;
            }
            await ProcessInput(userInput);
            //for folder inputs
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
                case "auth":// /auth
                    if (parameters.Length != 3)
                    {
                        Console.Error.WriteLine("ERR: Invalid number of parameters for /auth command");
                        return;
                    }
                    await ConnectToServer(parameters[0], parameters[1], parameters[2]);
                    break;
                case "join": // /join
                    if (parameters.Length != 1)
                    {
                        Console.Error.WriteLine("ERR: Invalid number of parameters for /join command");
                        return;
                    }
                    _joined = false;
                    JoinChannel(parameters[0]);
                    break;
                case "rename": // /rename
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
        else //write message to server
        {
            if (CurrentState == States.End || CurrentState == States.Error)
            {
                return;
            }
            else if (!_joined)
            {//for folder inputs
                Task.Delay(500).Wait();
            }
            else if (!_joined)
            {
                Console.Error.WriteLine($"ERR: You need to be in a channel to receive messages");
            }
            if (CurrentState == States.Open)
            {
                UDPMessage message = new UDPMessage((byte)UDPMsgType.MSG, userInput);
                message.DisplayName = DisplayName;
                //send MSG message
                _ = Task.Run(() => SendFunc(message, message.BuildMSG()));
            }
            else
            {
                Console.Error.WriteLine("ERR: You need to authenticate first");
            }
        }
    }
    //Function waits for BYE confirmation at the end of the communication
    public async Task WaitBYEconfirm(UDPMessage bye)
    {
        try
        {
            if (_messages == null || bye?.MessageID == null)
            {
                return;
            }
            //timer to avoid infinite loop
            DateTime startTime = DateTime.UtcNow;

            while (true)
            {
                var parsed = await ReadFromServer();
                if (parsed == null)
                {
                    return;
                }
                if (parsed.Type == (byte)UDPMsgType.CONFIRM && parsed.Ref_MessageID == bye.MessageID)
                {
                    HandleConfirm(parsed);
                    return;
                }
                //if no confirmation received, exit after 4x inserted delay
                if ((DateTime.UtcNow - startTime).TotalMilliseconds >= _delay * 4)
                {
                    Console.Error.WriteLine("ERR: No confirmation received");
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"ERR: {e.Message}");
            CleanResourcesAndExit();
        }
    }
    //Client sends BYE message and disconnects
    public override async Task DisconnectAndExit()
    {
        if (_dynamicPort != 0)
        {
            UDPMessage bye = new UDPMessage((byte)UDPMsgType.BYE);
            _ = Task.Run(() => WaitBYEconfirm(bye));
            await SendFunc(bye, bye.BuildBYE());
        }
        CleanResourcesAndExit();
    }
    //resource cleanup
    public void CleanResourcesAndExit()
    {
        try
        {
            if (UdpCli != null)
            {
                UdpCli.Close();
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
