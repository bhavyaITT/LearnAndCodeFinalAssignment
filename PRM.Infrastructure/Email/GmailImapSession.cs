using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace PRM.Infrastructure.Email;

/// <summary>
/// Minimal Gmail IMAP client (imap.gmail.com:993 + SSL) without third-party libraries.
/// </summary>
internal sealed class GmailImapSession : IAsyncDisposable
{
    private readonly TcpClient _tcp;
    private readonly SslStream _ssl;
    private readonly StreamReader _reader;
    private int _tag;

    private GmailImapSession(TcpClient tcp, SslStream ssl, StreamReader reader)
    {
        _tcp = tcp;
        _ssl = ssl;
        _reader = reader;
    }

    public static async Task<GmailImapSession> ConnectAndLoginAsync(
        string host,
        int port,
        string email,
        string appPassword,
        CancellationToken ct)
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct);

        var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = host
        }, ct);

        var reader = new StreamReader(ssl, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var session = new GmailImapSession(tcp, ssl, reader);

        await session.ReadServerGreetingAsync(ct);
        await session.ExecuteAsync($"LOGIN {Quote(email)} {Quote(appPassword)}", ct);
        return session;
    }

    public async Task<InboxSnapshot> ReadInboxSnapshotAsync(CancellationToken ct)
    {
        var statusResponse = await ExecuteAsync("STATUS INBOX (MESSAGES UNSEEN)", ct);
        var total = ParseStatusValue(statusResponse, "MESSAGES");
        var unread = ParseStatusValue(statusResponse, "UNSEEN");

        string? latestSubject = null;
        string? latestFrom = null;

        if (total > 0)
        {
            var fetchResponse = await ExecuteAsync(
                $"FETCH {total} (BODY.PEEK[HEADER.FIELDS (FROM SUBJECT)])",
                ct);
            latestFrom = ParseHeaderField(fetchResponse, "From");
            latestSubject = ParseHeaderField(fetchResponse, "Subject");
        }

        await ExecuteAsync("LOGOUT", ct);
        return new InboxSnapshot(total, unread, latestSubject, latestFrom);
    }

    private async Task ReadServerGreetingAsync(CancellationToken ct)
    {
        var line = await ReadLineAsync(ct);
        if (line is null || !line.StartsWith("* OK", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unexpected IMAP greeting: {line ?? "(null)"}");
    }

    private async Task<string> ExecuteAsync(string command, CancellationToken ct)
    {
        var tag = $"A{Interlocked.Increment(ref _tag):D4}";
        var payload = $"{tag} {command}\r\n";
        var bytes = Encoding.ASCII.GetBytes(payload);
        await _ssl.WriteAsync(bytes, ct);

        var response = new StringBuilder();
        while (true)
        {
            var line = await ReadLineAsync(ct);
            if (line is null)
                break;

            response.AppendLine(line);

            if (!line.StartsWith($"{tag} ", StringComparison.Ordinal))
                continue;

            if (line.Contains(" OK ", StringComparison.OrdinalIgnoreCase))
                break;

            if (line.Contains(" NO ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains(" BAD ", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"IMAP command failed: {line}");
            }
        }

        return response.ToString();
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var line = await _reader.ReadLineAsync(ct);
        if (line is null)
            return null;

        var literalMatch = Regex.Match(line, @"\{(\d+)\}\s*$");
        if (!literalMatch.Success)
            return line;

        var literalSize = int.Parse(literalMatch.Groups[1].Value);
        var literal = new char[literalSize];
        var read = 0;
        while (read < literalSize)
        {
            var chunk = await _reader.ReadBlockAsync(literal, read, literalSize - read);
            if (chunk == 0)
                break;
            read += chunk;
        }

        await _reader.ReadLineAsync(ct);
        return line + Environment.NewLine + new string(literal, 0, read);
    }

    private static string Quote(string value) =>
        $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static int ParseStatusValue(string response, string key)
    {
        var match = Regex.Match(response, $@"\b{key}\s+(\d+)\b", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static string? ParseHeaderField(string response, string headerName)
    {
        var match = Regex.Match(
            response,
            $@"{headerName}:\s*(.+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public async ValueTask DisposeAsync()
    {
        _reader.Dispose();
        await _ssl.DisposeAsync();
        _tcp.Dispose();
    }
}

internal sealed record InboxSnapshot(
    int TotalMessages,
    int UnreadMessages,
    string? LatestSubject,
    string? LatestFrom);
