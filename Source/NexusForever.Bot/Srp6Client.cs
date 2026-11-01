using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using NexusForever.Cryptography;

namespace NexusForever.Bot;

/// <summary>
/// Client half of the STS SRP6 exchange. The byte ordering, word reversal, padding,
/// and SHA interleave deliberately mirror Srp6Provider rather than a generic SRP library.
/// </summary>
internal sealed class Srp6Client
{
    private readonly BigInteger A;
    private readonly BigInteger M1;
    private readonly BigInteger K;

    public byte[] ClientCredentials => A.ToByteArray(true);
    public byte[] ClientEvidence => M1.ToByteArray(true);
    public byte[] SessionKey => K.ToByteArray(true);

    public Srp6Client(string identity, string password, byte[] salt, byte[] serverCredentials)
    {
        byte[] identityBytes = Encoding.UTF8.GetBytes(identity);
        var s = new BigInteger(salt, true);
        var B = new BigInteger(serverCredentials, true);
        if (B % Srp6Provider.N == BigInteger.Zero)
            throw new CryptographicException("STS supplied invalid SRP6 server credentials.");

        BigInteger a;
        do
        {
            a = RandomProvider.GetUnsignedBigInteger(128u);
        }
        while (a == BigInteger.Zero);

        A = BigInteger.ModPow(Srp6Provider.g, a, Srp6Provider.N);

        using SHA256 sha256 = SHA256.Create();
        byte[] passwordHash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{identity}:{password}"));
        BigInteger x = Hash(true, s, new BigInteger(passwordHash, true));
        BigInteger k = Hash(true, Srp6Provider.N, Srp6Provider.g);
        BigInteger u = Hash(true, A, B);

        BigInteger serverBase = (B - k * BigInteger.ModPow(Srp6Provider.g, x, Srp6Provider.N)) % Srp6Provider.N;
        if (serverBase < BigInteger.Zero)
            serverBase += Srp6Provider.N;
        if (serverBase == BigInteger.Zero)
            throw new CryptographicException("STS SRP6 shared-secret base is zero.");

        BigInteger S = BigInteger.ModPow(serverBase, a + u * x, Srp6Provider.N);
        K = ShaInterleave(S);

        byte[] identityHash = sha256.ComputeHash(identityBytes);
        M1 = Hash(false,
            Hash(false, Srp6Provider.N) ^ Hash(false, Srp6Provider.g),
            new BigInteger(identityHash, true),
            s,
            A,
            B,
            K);
    }

    public bool VerifyServerEvidence(byte[] serverEvidence)
    {
        BigInteger M2 = Hash(true, A, M1, K);
        byte[] expected = M2.ToByteArray(true);
        ReverseBytesAsUInt32(expected);
        return CryptographicOperations.FixedTimeEquals(expected, serverEvidence);
    }

    private static BigInteger Hash(bool reverse, params BigInteger[] integers)
    {
        using SHA256 sha256 = SHA256.Create();
        sha256.Initialize();

        for (int i = 0; i < integers.Length; i++)
        {
            byte[] buffer = integers[i].ToByteArray(true);
            int padding = buffer.Length % 4;
            if (padding != 0)
                Array.Resize(ref buffer, buffer.Length + (4 - padding));

            if (i == integers.Length - 1)
                sha256.TransformFinalBlock(buffer, 0, buffer.Length);
            else
                sha256.TransformBlock(buffer, 0, buffer.Length, null, 0);
        }

        byte[] hash = sha256.Hash ?? throw new CryptographicException("SHA-256 did not produce a hash.");
        if (reverse)
            ReverseBytesAsUInt32(hash);
        return new BigInteger(hash, true);
    }

    private static BigInteger ShaInterleave(BigInteger key)
    {
        byte[] keyBytes = key.ToByteArray(true);
        byte[] T = keyBytes.Reverse().ToArray();

        int first0 = Array.IndexOf<byte>(keyBytes, 0);
        int length = 4;
        if (first0 >= 0 && first0 < T.Length - 4)
            length = T.Length - first0;

        byte[] E = new byte[length / 2];
        for (uint i = 0u; i < E.Length; i++)
            E[i] = T[i * 2];

        byte[] F = new byte[length / 2];
        for (uint i = 0u; i < F.Length; i++)
            F[i] = T[i * 2 + 1];

        using SHA256 sha256 = SHA256.Create();
        byte[] G = sha256.ComputeHash(E);
        byte[] H = sha256.ComputeHash(F);

        byte[] sessionKey = new byte[G.Length + H.Length];
        for (uint i = 0u; i < sessionKey.Length; i++)
            sessionKey[i] = i % 2 == 0 ? G[i / 2] : H[i / 2];

        return new BigInteger(sessionKey, true);
    }

    private static void ReverseBytesAsUInt32(byte[] array)
    {
        int j = array.Length - 4;
        for (int i = 0; i < array.Length / 2; i += 4, j -= 4)
        {
            (array[i + 0], array[j + 0]) = (array[j + 0], array[i + 0]);
            (array[i + 1], array[j + 1]) = (array[j + 1], array[i + 1]);
            (array[i + 2], array[j + 2]) = (array[j + 2], array[i + 2]);
            (array[i + 3], array[j + 3]) = (array[j + 3], array[i + 3]);
        }
    }
}
