using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;
using NexusForever.Cryptography;
using NexusForever.Network.Message;

namespace NexusForever.Bot;

internal sealed record ProtocolPacket(GameMessageOpcode Opcode, byte[] Body);

internal sealed class ProtocolClient : IAsyncDisposable
{
    private const int MaximumFrameSize = 32 * 1024 * 1024;

    private readonly BotLog log;
    private readonly Channel<ProtocolPacket> packets = Channel.CreateUnbounded<ProtocolPacket>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);

    private TcpClient? tcpClient;
    private NetworkStream? stream;
    private Task? readTask;
    private PacketCrypt crypt = new(PacketCrypt.GetKeyFromAuthBuildAndMessage());
    private long lastActivityTicks = DateTime.UtcNow.Ticks;

    /// <summary>The last moment a frame was read from or written to the socket (the stall watchdog reads it).</summary>
    public DateTime LastActivity => new(Volatile.Read(ref lastActivityTicks), DateTimeKind.Utc);

    public bool Connected => tcpClient?.Connected == true && !lifetime.IsCancellationRequested;

    public ProtocolClient(BotLog log)
    {
        this.log = log;
    }

    public async Task ConnectAsync(Endpoint endpoint, int defaultPort, CancellationToken cancellationToken)
    {
        if (tcpClient is not null)
            throw new InvalidOperationException("Protocol client is already connected.");

        tcpClient = new TcpClient { NoDelay = true };
        await tcpClient.ConnectAsync(endpoint.Host, endpoint.GetPort(defaultPort), cancellationToken);
        stream = tcpClient.GetStream();
        readTask = Task.Run(ReadLoopAsync);
    }

    public void SetTicketKey(byte[] sessionKey)
    {
        crypt = new PacketCrypt(PacketCrypt.GetKeyFromTicket(sessionKey));
    }

    public async Task SendEncryptedAsync(GameMessageOpcode opcode, byte[] body, CancellationToken cancellationToken)
    {
        byte[] inner = new byte[sizeof(ushort) + body.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(inner, (ushort)opcode);
        body.CopyTo(inner.AsSpan(sizeof(ushort)));

        await sendLock.WaitAsync(cancellationToken);
        byte[] frame;
        try
        {
            if (stream is null)
                throw new InvalidOperationException("Protocol client is not connected.");

            // Keep frame construction, encryption, logging, and the socket write in one serialized
            // path so keep-alive and quest packets reach the wire and packet log in the same order.
            byte[] encrypted = crypt.Encrypt(inner, inner.Length);
            byte[] wrapperBody = new byte[sizeof(uint) + encrypted.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(wrapperBody, checked((uint)encrypted.Length + sizeof(uint)));
            encrypted.CopyTo(wrapperBody.AsSpan(sizeof(uint)));
            frame = BuildFrame(GameMessageOpcode.ClientEncrypted, wrapperBody);
            await stream.WriteAsync(frame, cancellationToken);
            Volatile.Write(ref lastActivityTicks, DateTime.UtcNow.Ticks);
            log.Packet("C->S", (ushort)opcode, inner.Length, "inner");
            log.Packet("C->S", (ushort)GameMessageOpcode.ClientEncrypted, frame.Length, "frame");
        }
        finally
        {
            sendLock.Release();
        }

    }

    public ValueTask<ProtocolPacket> ReadAsync(CancellationToken cancellationToken)
        => packets.Reader.ReadAsync(cancellationToken);

    private async Task ReadLoopAsync()
    {
        Exception? failure = null;
        try
        {
            if (stream is null)
                throw new InvalidOperationException("Protocol client is not connected.");

            byte[] sizeBytes = new byte[sizeof(uint)];
            while (!lifetime.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(sizeBytes, lifetime.Token);
                uint totalSize = BinaryPrimitives.ReadUInt32LittleEndian(sizeBytes);
                if (totalSize is < 6 or > MaximumFrameSize)
                    throw new InvalidDataException($"Invalid packet frame size {totalSize}.");

                byte[] remainder = new byte[totalSize - sizeof(uint)];
                await stream.ReadExactlyAsync(remainder, lifetime.Token);
                GameMessageOpcode outerOpcode = (GameMessageOpcode)BinaryPrimitives.ReadUInt16LittleEndian(remainder);
                byte[] outerBody = remainder[sizeof(ushort)..];
                Volatile.Write(ref lastActivityTicks, DateTime.UtcNow.Ticks);
                log.Packet("S->C", (ushort)outerOpcode, checked((int)totalSize), "frame");

                if (outerOpcode is GameMessageOpcode.ServerAuthEncrypted or GameMessageOpcode.ServerRealmEncrypted)
                {
                    if (outerBody.Length < sizeof(uint))
                        throw new InvalidDataException("Encrypted wrapper is missing its internal length.");

                    uint encryptedTotal = BinaryPrimitives.ReadUInt32LittleEndian(outerBody);
                    if (encryptedTotal < sizeof(uint) || encryptedTotal - sizeof(uint) != outerBody.Length - sizeof(uint))
                        throw new InvalidDataException($"Encrypted wrapper length {encryptedTotal} does not match frame payload {outerBody.Length}.");

                    byte[] encrypted = outerBody[sizeof(uint)..];
                    byte[] inner = crypt.Decrypt(encrypted, encrypted.Length);
                    if (inner.Length < sizeof(ushort))
                        throw new InvalidDataException("Encrypted packet is missing its inner opcode.");

                    GameMessageOpcode innerOpcode = (GameMessageOpcode)BinaryPrimitives.ReadUInt16LittleEndian(inner);
                    byte[] body = inner[sizeof(ushort)..];
                    log.Packet("S->C", (ushort)innerOpcode, inner.Length, "inner");
                    await packets.Writer.WriteAsync(new ProtocolPacket(innerOpcode, body), lifetime.Token);
                }
                else
                {
                    await packets.Writer.WriteAsync(new ProtocolPacket(outerOpcode, outerBody), lifetime.Token);
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            packets.Writer.TryComplete(failure);
        }
    }

    private static byte[] BuildFrame(GameMessageOpcode opcode, byte[] body)
    {
        int size = sizeof(uint) + sizeof(ushort) + body.Length;
        byte[] frame = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, checked((uint)size));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(sizeof(uint)), (ushort)opcode);
        body.CopyTo(frame.AsSpan(sizeof(uint) + sizeof(ushort)));
        return frame;
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        stream?.Close();
        tcpClient?.Dispose();
        if (readTask is not null)
        {
            try
            {
                await readTask;
            }
            catch
            {
            }
        }
        sendLock.Dispose();
        lifetime.Dispose();
    }
}
