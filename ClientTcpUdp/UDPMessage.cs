using System.Text;
using System.Text.RegularExpressions;


namespace ClientTcpUdp;
public class UDPMessage
{
    public byte Type { get; set; } //uint8
    public ushort? MessageID { get; set; }//unint16 idetifier
    public string? MessageContents { get; set; }

    public byte Result { get; set; }

    public ushort? Ref_MessageID { get; set; }//unint16
    public static ushort cnt = 0;

    public string? Username { get; set; }//20
    public string? ChannelID { get; set; }//20
    public string? Secret { get; set; }//128
    public string? DisplayName { get; set; }//20
    public bool _confirmed = false;

    public UDPMessage()
    {
    }

    public UDPMessage(byte type, string messageContents)
    {
        Type = type;
        MessageID = cnt++;//create msg to be send
        MessageContents = messageContents;
    }

    public UDPMessage(byte type)
    {
        Type = type;
        MessageID = cnt++;
    }
    public UDPMessage(byte type, string username, string displayName, string secret)
    {
        Type = type;
        MessageID = cnt++;
        Username = username;
        DisplayName = displayName;
        Secret = secret;
    }
    public UDPMessage(byte type, string displayName, string channel)
    {
        Type = type;
        MessageID = cnt++;
        DisplayName = displayName;
        ChannelID = channel;
    }
    //Function to check if the message is valid

    public string TypeToString()
    {
        switch (Type)
        {
            case (byte)UDPMsgType.CONFIRM:
                return "CONFIRM";
            case (byte)UDPMsgType.REPLY:
                return "REPLY";
            case (byte)UDPMsgType.AUTH:
                return "AUTH";
            case (byte)UDPMsgType.JOIN:
                return "JOIN";
            case (byte)UDPMsgType.MSG:
                return "MSG";
            case (byte)UDPMsgType.ERR:
                return "ERR";
            case (byte)UDPMsgType.BYE:
                return "BYE";
            default:
                break;
        }
        return "";
    }
    public bool CheckMessage()
    {
        switch (Type)
        {
            case (byte)UDPMsgType.AUTH:
                if (Username != null && DisplayName != null && Secret != null)
                {
                    if(Username.Length > 20 || DisplayName.Length > 20 || Secret.Length > 128)
                    {
                        return false;
                    }
                    Match username = Regex.Match(Username, Grammar.ID);
                    Match displayName = Regex.Match(DisplayName, Grammar.DNAME);
                    Match secret = Regex.Match(Secret, Grammar.SECRET);
                    if (username.Success && displayName.Success && secret.Success)
                    {
                        return true;
                    }
                }
                break;
            case (byte)UDPMsgType.JOIN:
                if (ChannelID != null && DisplayName != null)
                {
                    if(ChannelID.Length > 20 || DisplayName.Length > 20)
                    {
                        return false;
                    }
                    Match channelID = Regex.Match(ChannelID, Grammar.ID);
                    Match displayName = Regex.Match(DisplayName, Grammar.DNAME);
                    if (channelID.Success && displayName.Success)
                    {
                        return true;
                    }
                }
                break;
            case (byte)UDPMsgType.ERR:
                if (DisplayName != null && MessageContents != null)
                {
                    if(DisplayName.Length > 20 || MessageContents.Length > 1400)
                    {
                        return false;
                    }
                    Match displayName = Regex.Match(DisplayName, Grammar.DNAME);
                    Match messageContents = Regex.Match(MessageContents, Grammar.CONTENT);
                    if (displayName.Success && messageContents.Success)
                    {
                        return true;
                    }
                }
                break;
            case (byte)UDPMsgType.MSG:
                if (DisplayName != null && MessageContents != null)
                {
                    if(DisplayName.Length > 20 || MessageContents.Length > 1400)
                    {
                        return false;
                    }
                    Match displayName = Regex.Match(DisplayName, Grammar.DNAME);
                    Match messageContents = Regex.Match(MessageContents, Grammar.CONTENT);
                    if (displayName.Success && messageContents.Success)
                    {
                        return true;
                    }
                }
                break;
            case (byte)UDPMsgType.REPLY:
                if ((Result == 0 || Result == 1) && (MessageContents != null))
                {
                    if(MessageContents.Length > 1400)
                    {
                        return false;
                    }
                    Match messageContents = Regex.Match(MessageContents, Grammar.CONTENT);
                    if (messageContents.Success)
                    {
                        return true;
                    }
                }
                break;
            default:
                break;
        }
        return false;
    }
    
    //Function to parse the message
    public bool ParseMessage(byte[] data)
    {
        if (data.Length < 3)
        {
            return false;
        }
        Type = data[0];
        if (Type == (byte)UDPMsgType.CONFIRM)
        {
            Ref_MessageID = BitConverter.ToUInt16(data[1..3]);
            // Console.WriteLine($"Type = {Type}, Ref_MessageID = {Ref_MessageID}");
            if (data.Length == 3)
            {
                return true;
            }
        }

        MessageID = BitConverter.ToUInt16(data[1..3]);
        if (Type == (byte)UDPMsgType.BYE)
        {
            // Console.WriteLine($"Type = {Type}, MessageID = {MessageID}");
            if (data.Length == 3)
            {
                return true;
            }
        }
        if (data.Length > 3)
        {
            bool retVal = false;
            switch (Type)
            {
                case (byte)UDPMsgType.REPLY:
                    Result = data[3];
                    Ref_MessageID = BitConverter.ToUInt16(data[4..6]);

                    var parts = Encoding.ASCII.GetString(data[6..]).Split('\0');
                    if (parts.Length != 2)
                    {
                        return false;
                    }
                    MessageContents = parts[0];
                    // Console.WriteLine($"Type = {Type}, MessageID = {MessageID}, Result = {Result}, Ref_MessageID = {Ref_MessageID}");
                    retVal = CheckMessage();
                    break;
                case (byte)UDPMsgType.AUTH:
                    parts = Encoding.ASCII.GetString(data[3..]).Split('\0');
                    if (parts.Length != 4)
                    {
                        return false;
                    }
                    Username = parts[0];
                    DisplayName = parts[1];
                    Secret = parts[2];
                    // Console.WriteLine($"Type = {Type}, MessageID = {MessageID}, Username = {Username}, DisplayName = {DisplayName}, Secret = {Secret}");
                    retVal = CheckMessage();
                    break;
                case (byte)UDPMsgType.JOIN:
                    parts = Encoding.ASCII.GetString(data[3..]).Split('\0');
                    if (parts.Length != 3)
                    {
                        return false;
                    }
                    ChannelID = parts[0];
                    DisplayName = parts[1];
                    // Console.WriteLine($"Type = {Type}, MessageID = {MessageID}, ChannelID = {ChannelID}, DisplayName = {DisplayName}");
                    retVal = CheckMessage();
                    break;
                case (byte)UDPMsgType.MSG:
                    parts = Encoding.ASCII.GetString(data[3..]).Split('\0');
                    if (parts.Length != 3)
                    {
                        return false;
                    }
                    DisplayName = parts[0];
                    MessageContents = parts[1];
                    // Console.WriteLine($"Type = {Type}, MessageID = {MessageID}, DisplayName = {DisplayName}, MessageContents = {MessageContents}");
                    retVal = CheckMessage();
                    break;
                case (byte)UDPMsgType.ERR:
                    parts = Encoding.ASCII.GetString(data[3..]).Split('\0');
                    if (parts.Length != 3)
                    {
                        return false;
                    }
                    DisplayName = parts[0];
                    MessageContents = parts[1];
                    // Console.WriteLine($"Type = {Type}, MessageID = {MessageID}, DisplayName = {DisplayName}, MessageContents = {MessageContents}");
                    retVal = CheckMessage();
                    break;
                default:
                    return false;
            }
            return retVal;
        }
        return false;
    }

    public byte[] BuildCONFIRM()
    {
        List<byte> byteArray = new List<byte>();

        if (Ref_MessageID == null)
        {
            Console.WriteLine("Error: Message is not complete");
            return byteArray.ToArray();
        }

        byteArray.Add((byte)UDPMsgType.CONFIRM);
        byteArray.AddRange(BitConverter.GetBytes(Ref_MessageID.Value));
        return byteArray.ToArray();
    }
    public byte[] BuildAUTH()
    {
        List<byte> byteArray = new List<byte>();

        if (MessageID == null || Username == null || DisplayName == null || Secret == null)
        {
            Console.WriteLine("Error: Message is not complete");
            return byteArray.ToArray();
        }

        byteArray.Add((byte)UDPMsgType.AUTH);
        byteArray.AddRange(BitConverter.GetBytes(MessageID.Value));

        byteArray.AddRange(Encoding.ASCII.GetBytes(Username));
        byteArray.Add(0x00);
        byteArray.AddRange(Encoding.ASCII.GetBytes(DisplayName));
        byteArray.Add(0x00);
        byteArray.AddRange(Encoding.ASCII.GetBytes(Secret));
        byteArray.Add(0x00);
        return byteArray.ToArray();

    }
    public byte[] BuildJOIN()
    {
        List<byte> byteArray = new List<byte>();

        if (MessageID == null || ChannelID == null || DisplayName == null)
        {
            Console.WriteLine("Error: Message is not complete");
            return byteArray.ToArray();
        }

        byteArray.Add((byte)UDPMsgType.JOIN);
        byteArray.AddRange(BitConverter.GetBytes(MessageID.Value));

        byteArray.AddRange(Encoding.ASCII.GetBytes(ChannelID));
        byteArray.Add(0x00);
        byteArray.AddRange(Encoding.ASCII.GetBytes(DisplayName));
        byteArray.Add(0x00);
        return byteArray.ToArray();
    }
    public byte[] BuildMSG()
    {
        List<byte> byteArray = new List<byte>();

        if (MessageID == null || DisplayName == null || MessageContents == null)
        {
            Console.WriteLine("Error: Message is not complete");
            return byteArray.ToArray();
        }

        byteArray.Add((byte)UDPMsgType.MSG);
        byteArray.AddRange(BitConverter.GetBytes(MessageID.Value));

        byteArray.AddRange(Encoding.ASCII.GetBytes(DisplayName));
        byteArray.Add(0x00);
        byteArray.AddRange(Encoding.ASCII.GetBytes(MessageContents));
        byteArray.Add(0x00);
        return byteArray.ToArray();
    }
    public byte[] BuildERR()
    {
        List<byte> byteArray = new List<byte>();

        if (MessageID == null || DisplayName == null || MessageContents == null)
        {
            Console.WriteLine("Error: Message is not complete");
            return byteArray.ToArray();
        }

        byteArray.Add((byte)UDPMsgType.ERR);
        byteArray.AddRange(BitConverter.GetBytes(MessageID.Value));

        byteArray.AddRange(Encoding.ASCII.GetBytes(DisplayName));
        byteArray.Add(0x00);
        byteArray.AddRange(Encoding.ASCII.GetBytes(MessageContents));
        byteArray.Add(0x00);
        return byteArray.ToArray();
    }
    public byte[] BuildBYE()
    {
        List<byte> byteArray = new List<byte>();

        if (MessageID == null)
        {
            Console.WriteLine("Error: Message is not complete");
            return byteArray.ToArray();
        }

        byteArray.Add((byte)UDPMsgType.BYE);
        byteArray.AddRange(BitConverter.GetBytes(MessageID.Value));
        return byteArray.ToArray();
    }

}

public enum UDPMsgType
{
    CONFIRM = 0x00,
    REPLY = 0x01,
    AUTH = 0x02,
    JOIN = 0x03,
    MSG = 0x04,
    ERR = 0xFE,
    BYE = 0xFF
}