
namespace ClientTcpUdp;
public class Grammar
{
    //input from user
    public const string ID = @"[A-Za-z0-9\-.]{1,20}";//Username, ChannelID
    public const string SECRET = @"[A-Za-z0-9\-]{1,128}";//Secret
    public const string CONTENT = @"[\x20-\x7E]{1,1400}";//MessageContent
    public const string DNAME = @"[\x21-\x7E]{1,20}";//DisplayName
    // connection between words
    public const string IS = @"\sIS\s";
    public const string AS = @"\sAS\s";
    public const string USING = @"\sUSING\s";
    //commands
    public const string ContentJoin = @"JOIN\s" + ID + AS + DNAME;
    public const string ContentAuth = @"AUTH\s" + ID + AS + DNAME + USING + SECRET;
    public const string ContentMessage = @"MSG\sFROM\s" + DNAME + IS + CONTENT;
    public const string ContentError = @"ERR\sFROM\s" + DNAME + IS + CONTENT;
    public const string ContentReply = @"REPLY\s(OK|NOK)"+ IS + CONTENT;
    public const string ContentBye = @"BYE";
    //all commands combined
    public const string Content = ContentAuth + "|" + ContentJoin + "|" + ContentMessage + "|" + ContentError + "|" + ContentReply + "|" + ContentBye;
    //final message
    public const string Message = @"^(" + Content + @")" + "\r\n" + @"$";
}