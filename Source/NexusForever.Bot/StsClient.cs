using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using NexusForever.Cryptography;

namespace NexusForever.Bot;

internal sealed class StsClient : IAsyncDisposable
{
    private const int DefaultPort = 6600;
    private const int MaximumMessageSize = 1024 * 1024;

    private readonly Endpoint endpoint;
    private readonly string identity;
    private readonly string password;
    private readonly List<byte> receiveBuffer = [];

    private TcpClient? tcpClient;
    private NetworkStream? stream;
    private Arc4Provider? clientEncryption;
    private Arc4Provider? serverEncryption;
    private Srp6Client? keyExchange;
    private uint sequence = 1u;

    public Guid GameToken { get; private set; }

    public StsClient(Endpoint endpoint, string identity, string password)
    {
        this.endpoint = endpoint;
        this.identity = identity.Trim().ToLowerInvariant();
        this.password = password;
    }

    public async Task<string> ConnectAsync(CancellationToken cancellationToken)
    {
        if (tcpClient is not null)
            throw new InvalidOperationException("STS client is already connected.");

        tcpClient = new TcpClient { NoDelay = true };
        await tcpClient.ConnectAsync(endpoint.Host, endpoint.GetPort(DefaultPort), cancellationToken);
        stream = tcpClient.GetStream();

        string body = new XElement("Connect", new XElement("ConnType", 0)).ToString(SaveOptions.DisableFormatting);
        await SendOnlyAsync("/Sts/Connect", body, cancellationToken);
        return "status=sent; connected and sent /Sts/Connect (the server handler has no response)";
    }

    public async Task<string> LoginStartAsync(CancellationToken cancellationToken)
    {
        string body = new XElement("Request", new XElement("LoginName", identity)).ToString(SaveOptions.DisableFormatting);
        StsResponse response = await SendAsync("/Auth/LoginStart", body, cancellationToken);
        try
        {
            byte[] encoded = ReadKeyDataElement(response.Body);
            int offset = 0;
            byte[] salt = ReadLengthPrefixed(encoded, ref offset, "salt");
            byte[] serverCredentials = ReadLengthPrefixed(encoded, ref offset, "server credentials");
            EnsureConsumed(encoded, offset);
            keyExchange = new Srp6Client(identity, password, salt, serverCredentials);
            return response.StatusLine;
        }
        catch (Exception exception) when (exception is not StsProtocolException)
        {
            throw new StsProtocolException($"{response.StatusLine}; invalid login-start response: {exception.Message}", exception);
        }
    }

    public async Task<string> SendKeyDataAsync(CancellationToken cancellationToken)
    {
        Srp6Client exchange = keyExchange ?? throw new InvalidOperationException("STS login start has not completed.");
        byte[] keyData = BuildKeyData(exchange.ClientCredentials, exchange.ClientEvidence);
        string body = new XElement("Request",
            new XElement("KeyData", Convert.ToBase64String(keyData))).ToString(SaveOptions.DisableFormatting);

        StsResponse response = await SendAsync("/Auth/KeyData", body, cancellationToken);
        try
        {
            byte[] encoded = ReadKeyDataElement(response.Body);
            int offset = 0;
            byte[] serverEvidence = ReadLengthPrefixed(encoded, ref offset, "server evidence");
            EnsureConsumed(encoded, offset);
            if (!exchange.VerifyServerEvidence(serverEvidence))
                throw new StsProtocolException($"{response.StatusLine}; invalid SRP6 server evidence M2");

            byte[] sessionKey = exchange.SessionKey;
            clientEncryption = new Arc4Provider(sessionKey);
            serverEncryption = new Arc4Provider(sessionKey);
            return response.StatusLine;
        }
        catch (Exception exception) when (exception is not StsProtocolException)
        {
            throw new StsProtocolException($"{response.StatusLine}; invalid key-data response: {exception.Message}", exception);
        }
    }

    public async Task<string> LoginFinishAsync(CancellationToken cancellationToken)
    {
        EnsureEncryptionActive();
        StsResponse response = await SendAsync("/Auth/LoginFinish", "<Request/>", cancellationToken);
        return response.StatusLine;
    }

    public async Task<string> RequestGameTokenAsync(CancellationToken cancellationToken)
    {
        EnsureEncryptionActive();
        StsResponse response = await SendAsync("/Auth/RequestGameToken", "<Request/>", cancellationToken);
        try
        {
            XDocument document = XDocument.Parse(response.Body);
            string value = document.Root?.Element("Token")?.Value
                ?? throw new InvalidDataException("Reply/Token is missing.");
            if (!Guid.TryParse(value, out Guid token) || token == Guid.Empty)
                throw new InvalidDataException("Reply/Token is not a non-empty GUID.");
            GameToken = token;
            return response.StatusLine;
        }
        catch (Exception exception) when (exception is not StsProtocolException)
        {
            throw new StsProtocolException($"{response.StatusLine}; invalid game-token response: {exception.Message}", exception);
        }
    }

    private async Task<StsResponse> SendAsync(string uri, string body, CancellationToken cancellationToken)
    {
        await SendOnlyAsync(uri, body, cancellationToken);
        StsResponse response = await ReadResponseAsync(cancellationToken);
        if (response.StatusCode != 200u)
            throw new StsProtocolException(response.StatusLine);
        return response;
    }

    private async Task SendOnlyAsync(string uri, string body, CancellationToken cancellationToken)
    {
        if (stream is null)
            throw new InvalidOperationException("STS client is not connected.");

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string header = $"POST {uri} STS/1.0\r\nl:{bodyBytes.Length}\r\ns:{sequence++}\r\n\r\n";
        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        byte[] message = new byte[headerBytes.Length + bodyBytes.Length];
        headerBytes.CopyTo(message, 0);
        bodyBytes.CopyTo(message, headerBytes.Length);
        clientEncryption?.Encrypt(message);
        await stream.WriteAsync(message, cancellationToken);
    }

    private async Task<StsResponse> ReadResponseAsync(CancellationToken cancellationToken)
    {
        if (stream is null)
            throw new InvalidOperationException("STS client is not connected.");

        byte[] raw = new byte[4096];
        while (true)
        {
            StsResponse? buffered = TryTakeResponse();
            if (buffered is not null)
                return buffered;

            int count = await stream.ReadAsync(raw, cancellationToken);
            if (count == 0)
                throw new EndOfStreamException("STS server closed the connection while a response was pending.");

            byte[] received = raw.AsSpan(0, count).ToArray();
            serverEncryption?.Decrypt(received);
            receiveBuffer.AddRange(received);
            if (receiveBuffer.Count > MaximumMessageSize)
                throw new InvalidDataException($"STS response exceeds {MaximumMessageSize} bytes.");
        }
    }

    private StsResponse? TryTakeResponse()
    {
        int headerEnd = FindHeaderEnd(receiveBuffer);
        if (headerEnd < 0)
            return null;

        string header = Encoding.UTF8.GetString(receiveBuffer.GetRange(0, headerEnd).ToArray());
        string[] lines = header.Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
            throw new InvalidDataException("STS response has no status line.");

        string statusLine = lines[0];
        string[] statusParts = statusLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (statusParts.Length < 3 || statusParts[0] != "STS/1.0" || !uint.TryParse(statusParts[1], out uint statusCode))
            throw new InvalidDataException($"Invalid STS status line: {statusLine}");

        int bodyLength = -1;
        foreach (string line in lines.Skip(1))
        {
            int separator = line.IndexOf(':');
            if (separator <= 0)
                throw new InvalidDataException($"Invalid STS response header: {line}");
            if (line[..separator] == "l" && !int.TryParse(line[(separator + 1)..], out bodyLength))
                throw new InvalidDataException($"Invalid STS response length: {line}");
        }
        if (bodyLength is < 0 or > MaximumMessageSize)
            throw new InvalidDataException("STS response has a missing or invalid length header.");

        int bodyStart = headerEnd + 4;
        int messageLength = checked(bodyStart + bodyLength);
        if (receiveBuffer.Count < messageLength)
            return null;

        string body = Encoding.UTF8.GetString(receiveBuffer.GetRange(bodyStart, bodyLength).ToArray());
        receiveBuffer.RemoveRange(0, messageLength);
        return new StsResponse(statusLine, statusCode, body);
    }

    private static int FindHeaderEnd(IReadOnlyList<byte> buffer)
    {
        for (int i = 0; i <= buffer.Count - 4; i++)
        {
            if (buffer[i] == (byte)'\r' && buffer[i + 1] == (byte)'\n'
                && buffer[i + 2] == (byte)'\r' && buffer[i + 3] == (byte)'\n')
                return i;
        }
        return -1;
    }

    private static byte[] BuildKeyData(byte[] clientCredentials, byte[] clientEvidence)
    {
        byte[] result = new byte[sizeof(int) + clientCredentials.Length + sizeof(int) + clientEvidence.Length];
        int offset = 0;
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), clientCredentials.Length);
        offset += sizeof(int);
        clientCredentials.CopyTo(result, offset);
        offset += clientCredentials.Length;
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(offset), clientEvidence.Length);
        offset += sizeof(int);
        clientEvidence.CopyTo(result, offset);
        return result;
    }

    private static byte[] ReadKeyDataElement(string body)
    {
        XDocument document = XDocument.Parse(body);
        string value = document.Root?.Element("KeyData")?.Value
            ?? throw new InvalidDataException("Reply/KeyData is missing.");
        return Convert.FromBase64String(value);
    }

    private static byte[] ReadLengthPrefixed(byte[] data, ref int offset, string field)
    {
        if (data.Length - offset < sizeof(int))
            throw new InvalidDataException($"KeyData is missing the {field} length.");
        int length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += sizeof(int);
        if (length < 0 || length > data.Length - offset)
            throw new InvalidDataException($"KeyData has an invalid {field} length {length}.");
        byte[] value = data.AsSpan(offset, length).ToArray();
        offset += length;
        return value;
    }

    private static void EnsureConsumed(byte[] data, int offset)
    {
        if (offset != data.Length)
            throw new InvalidDataException($"KeyData has {data.Length - offset} trailing byte(s).");
    }

    private void EnsureEncryptionActive()
    {
        if (clientEncryption is null || serverEncryption is null)
            throw new InvalidOperationException("STS encryption is not active.");
    }

    public ValueTask DisposeAsync()
    {
        stream?.Dispose();
        tcpClient?.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed record StsResponse(string StatusLine, uint StatusCode, string Body);
}

internal sealed class StsProtocolException : Exception
{
    public StsProtocolException(string message)
        : base(message)
    {
    }

    public StsProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
