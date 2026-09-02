using Riptide;
using System.Text;

public sealed class ChatService
{
    private const int MaximumMessageLength = 160;
    private const ushort SystemSenderId = 0;
    private const string SystemSenderName = "System";
    private static readonly TimeSpan MinimumSendInterval = TimeSpan.FromMilliseconds(350);

    private readonly IDictionary<ushort, PlayerSession> sessions;
    private readonly Riptide.Server server;
    private readonly Func<PlayerSession, bool> canSend;
    private readonly Dictionary<ushort, DateTime> lastSentAtUtc = new();

    public ChatService(
        IDictionary<ushort, PlayerSession> sessions,
        Riptide.Server server,
        Func<PlayerSession, bool> canSend)
    {
        this.sessions = sessions;
        this.server = server;
        this.canSend = canSend;
    }

    public bool TryHandleMessage(
        ushort clientId,
        NetworkMessageId messageId,
        Message message)
    {
        if (messageId != NetworkMessageId.ChatSend)
            return false;

        if (!sessions.TryGetValue(clientId, out PlayerSession? session) ||
            !canSend(session))
        {
            return true;
        }

        string text = NormalizeMessage(message.GetString());
        if (text.Length == 0)
            return true;

        DateTime nowUtc = DateTime.UtcNow;
        if (lastSentAtUtc.TryGetValue(clientId, out DateTime lastSentAt) &&
            nowUtc - lastSentAt < MinimumSendInterval)
        {
            return true;
        }

        lastSentAtUtc[clientId] = nowUtc;
        Broadcast(session, text);
        return true;
    }

    public void BroadcastSimulatedMessage(PlayerSession sender, string text)
    {
        if (!sender.IsSimulated)
            return;

        text = NormalizeMessage(text);
        if (text.Length > 0)
            Broadcast(sender, text);
    }

    public void BroadcastSystemMessage(string text)
    {
        text = NormalizeMessage(text);
        if (text.Length == 0)
            return;

        foreach (PlayerSession recipient in sessions.Values)
        {
            if (!recipient.IsSimulated)
                SendBroadcast(recipient.ClientId, SystemSenderId, SystemSenderName, text);
        }
    }

    public void SendSystemMessage(ushort clientId, string text)
    {
        if (!sessions.TryGetValue(clientId, out PlayerSession? recipient) || recipient.IsSimulated)
            return;

        text = NormalizeMessage(text);
        if (text.Length > 0)
            SendBroadcast(clientId, SystemSenderId, SystemSenderName, text);
    }

    public void RemoveClient(ushort clientId)
    {
        lastSentAtUtc.Remove(clientId);
    }

    private void Broadcast(PlayerSession sender, string text)
    {
        foreach (PlayerSession recipient in sessions.Values)
        {
            if (!recipient.IsSimulated)
                SendBroadcast(recipient.ClientId, sender.ClientId, sender.Username, text);
        }
    }

    private void SendBroadcast(
        ushort recipientClientId,
        ushort senderClientId,
        string senderName,
        string text)
    {
        Message message = Message.Create(
            MessageSendMode.Reliable,
            NetworkMessageId.ChatBroadcast);
        message.AddUShort(senderClientId);
        message.AddString(senderName);
        message.AddString(text);
        server.Send(message, recipientClientId);
    }

    private static string NormalizeMessage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder builder = new(Math.Min(value.Length, MaximumMessageLength));
        bool previousWasWhitespace = false;
        foreach (char character in value.Trim())
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(character);
                previousWasWhitespace = false;
            }

            if (builder.Length >= MaximumMessageLength)
                break;
        }

        return builder.ToString().TrimEnd();
    }
}