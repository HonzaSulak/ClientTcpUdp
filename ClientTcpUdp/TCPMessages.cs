using System.Text.RegularExpressions;

namespace ClientTcpUdp;
public class Message
{
    public ushort MessageID { get; set; }//uint16
    public string? Username { get; set; }//20
    public string? ChannelID { get; set; }//20
    public string? Secret { get; set; }//128
    public string? DisplayName { get; set; }//20
    public string? MessageContent { get; set; }//1400
    public static ushort cnt = 1;
    public MsgTypes MessageType { get; set; }

    public Message()
    {
        this.MessageID = cnt++;
        this.MessageType = MsgTypes.ERR;
    }
    public static bool CheckMessage(string message)
    {
        Match match = Regex.Match(message, Grammar.Message);
        if (match.Success)
        {
            return true;
        }
        return false;
    }

    //Function to parse message
    public bool ParseMessage(string? message)
    {
        bool retVal = false;
        if (message != null && CheckMessage(message))
        {
            retVal = true;
            string[] parts = message.Split('\r');
            parts = parts[0].Split(' ');
            switch (parts[0])
            {
                case "AUTH":
                    this.MessageType = MsgTypes.AUTH;
                    this.Username = parts[1];
                    this.DisplayName = parts[3];
                    this.Secret = parts[5];
                    break;
                case "JOIN":
                    this.MessageType = MsgTypes.JOIN;
                    this.ChannelID = parts[1];
                    this.DisplayName = parts[3];
                    break;
                case "MSG":
                    this.MessageType = MsgTypes.MSG;
                    this.DisplayName = parts[2];
                    this.MessageContent = string.Join(" ", parts[4..]);
                    break;
                case "ERR":
                    this.MessageType = MsgTypes.ERR;
                    this.DisplayName = parts[2];
                    this.MessageContent = string.Join(" ", parts[4..]);
                    break;
                case "REPLY":
                    if (parts[1] == "OK")
                    {
                        this.MessageType = MsgTypes.REPLYok;
                    }
                    else
                    {
                        this.MessageType = MsgTypes.REPLYnok;
                    }
                    this.MessageContent = string.Join(" ", parts[3..]);
                    break;
                case "BYE":
                    this.MessageType = MsgTypes.BYE;
                    break;
                default:
                    break;
            }
        }
        return retVal;
    }

    public static string BuildAUTH(string username, string displayName, string secret)
    {
        string message = $"AUTH {username} AS {displayName} USING {secret}\r\n";
        if (CheckMessage(message))
        {
            return message;
        }
        return "---";
    }
    public static string BuildJOIN(string ChannelID, string displayName)
    {
        string message = $"JOIN {ChannelID} AS {displayName}\r\n";
        if (CheckMessage(message))
        {
            return message;
        }
        return "---";
    }
    public static string BuildMSG(string displayName, string messageContent)
    {
        string message = $"MSG FROM {displayName} IS {messageContent}\r\n";
        if (CheckMessage(message))
        {
            return message;
        }
        return "---";
    }
    public static string BuildERR(string displayName, string messageContent)
    {
        string message = $"ERR FROM {displayName} IS {messageContent}\r\n";
        if (CheckMessage(message))
        {
            return message;
        }
        return "---";
    }
    public static string BuildREPLYok(string messageContent)
    {
        string message = $"REPLY OK IS {messageContent}\r\n";
        if (CheckMessage(message))
        {
            return message;
        }
        return "---";
    }
    public static string BuildREPLYnok(string messageContent)
    {
        string message = $"REPLY NOK IS {messageContent}\r\n";
        if (CheckMessage(message))
        {
            return message;
        }
        return "---";
    }
    public static string BuildBYE()
    {
        string message = "BYE\r\n";
        if (CheckMessage(message))
        {
            return message;
        }
        return "---";
    }
}
public enum MsgTypes
{
    ERR,
    REPLYok,
    REPLYnok,
    AUTH,
    JOIN,
    MSG,
    BYE
}
