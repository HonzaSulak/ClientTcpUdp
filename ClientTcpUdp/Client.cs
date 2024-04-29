namespace ClientTcpUdp;
//parent class for TCP and UDP clients
public abstract class Client
{
    public States CurrentState { get; set; }
    public string Username { get; set; }
    public string DisplayName { get; set; }
    public string Secret { get; set; }
    public string ChannelID { get; set; }
    public string NextChannel { get; set; }

    protected bool _joined = true;

    protected Client()
    {
        CurrentState = States.Start; // initial state
        Username = "";
        DisplayName = "";
        Secret = "";
        ChannelID = "";
        NextChannel = "";
    }
    //abstract methods
    public abstract Task ConnectToServer(string username, string displayName, string secret);

    public abstract Task ReadMessages();

    public abstract Task ReadPackets();

    public abstract Task ProcessInput(string userInput);

    public abstract Task WritePackets();

    public abstract Task DisconnectAndExit();
    // /rename command
    public void RenameCommand(string[] parameters)
    {
        if (CurrentState != States.Open && CurrentState != States.Auth)
        {
            Console.Error.WriteLine("ERR: You need to authenticate first");
            return;
        }
        if (parameters.Length != 1)
        {
            Console.Error.WriteLine("ERR: Invalid number of parameters for /rename command");
            return;
        }
        DisplayName = parameters[0];
    }
    // Prints out supported local commands when /help is entered
    public static void PrintHelp()
    {
        Console.WriteLine("Supported local commands:");
        Console.WriteLine("/auth\t{Username} {Secret} {DisplayName}\tSends AUTH message to the server");
        Console.WriteLine("/join\t{ChannelID}\t\t\t\tSends JOIN message to the server");
        Console.WriteLine("/rename\t{DisplayName}\t\t\t\tChanges the display name of the user locally");
        Console.WriteLine("/help\t\t\t\t\t\tPrints out supported local commands");
    }
}

public enum States
{
    Start,
    Auth,
    Open,
    Error,
    End
}
