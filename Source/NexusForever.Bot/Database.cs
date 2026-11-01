using MySqlConnector;
using NexusForever.Cryptography;

namespace NexusForever.Bot;

internal sealed record Spawn(uint Id, uint CreatureId, ushort WorldId, float X, float Y, float Z)
{
    public double DistanceSquared(float x, float y, float z)
    {
        double dx = X - x;
        double dy = Y - y;
        double dz = Z - z;
        return dx * dx + dy * dy + dz * dz;
    }
}

internal static class BotDatabase
{
    private const string AuthDatabase = "nexus_forever_auth";
    private const string CharacterDatabase = "nexus_forever_character";
    private const string WorldDatabase = "nexus_forever_world";

    public static async Task<uint> PrepAsync(PrepOptions options, CancellationToken cancellationToken)
    {
        string email = options.Email.Trim().ToLowerInvariant();
        (string salt, string verifier) = PasswordProvider.GenerateSaltAndVerifier(email, options.Password);
        uint accountId;

        await using (var connection = new MySqlConnection(ForDatabase(options.Database, AuthDatabase)))
        {
            await connection.OpenAsync(cancellationToken);
            await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var select = new MySqlCommand("SELECT id FROM account WHERE email = @email ORDER BY id LIMIT 1 FOR UPDATE", connection, transaction))
            {
                select.Parameters.AddWithValue("@email", email);
                object? existing = await select.ExecuteScalarAsync(cancellationToken);
                if (existing is null)
                {
                    await using var insert = new MySqlCommand(
                        "INSERT INTO account (email, s, v, gameToken, sessionKey) VALUES (@email, @salt, @verifier, '', '')", connection, transaction);
                    insert.Parameters.AddWithValue("@email", email);
                    insert.Parameters.AddWithValue("@salt", salt);
                    insert.Parameters.AddWithValue("@verifier", verifier);
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                    accountId = checked((uint)insert.LastInsertedId);
                }
                else
                {
                    accountId = Convert.ToUInt32(existing);
                    await using var update = new MySqlCommand(
                        "UPDATE account SET s = @salt, v = @verifier, gameToken = '', sessionKey = '' WHERE id = @id", connection, transaction);
                    update.Parameters.AddWithValue("@salt", salt);
                    update.Parameters.AddWithValue("@verifier", verifier);
                    update.Parameters.AddWithValue("@id", accountId);
                    await update.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await using (var role = new MySqlCommand("INSERT IGNORE INTO account_role (id, roleId) VALUES (@id, 3)", connection, transaction))
            {
                role.Parameters.AddWithValue("@id", accountId);
                await role.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var connection = new MySqlConnection(ForDatabase(options.Database, CharacterDatabase)))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                UPDATE `character`
                SET deleteTime = UTC_TIMESTAMP(), originalName = COALESCE(originalName, name), name = NULL, isOnline = 0
                WHERE accountId = @id AND deleteTime IS NULL AND name REGEXP '^Bot ?[0-9]{4}$'
                """, connection);
            command.Parameters.AddWithValue("@id", accountId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return accountId;
    }

    public static async Task<IReadOnlyList<Spawn>> LoadSpawnsAsync(
        string connectionString,
        IEnumerable<uint> creatureIds,
        CancellationToken cancellationToken)
    {
        uint[] ids = creatureIds.Where(id => id != 0).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        await using var connection = new MySqlConnection(ForDatabase(connectionString, WorldDatabase));
        await connection.OpenAsync(cancellationToken);

        var parameterNames = new List<string>(ids.Length);
        await using var command = connection.CreateCommand();
        for (int i = 0; i < ids.Length; i++)
        {
            string name = $"@c{i}";
            parameterNames.Add(name);
            command.Parameters.AddWithValue(name, ids[i]);
        }

        command.CommandText = $"SELECT id, creature, world, x, y, z FROM entity WHERE creature IN ({string.Join(',', parameterNames)}) ORDER BY world, creature, id";
        var result = new List<Spawn>();
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Spawn(
                reader.GetFieldValue<uint>(0),
                reader.GetFieldValue<uint>(1),
                reader.GetFieldValue<ushort>(2),
                reader.GetFloat(3),
                reader.GetFloat(4),
                reader.GetFloat(5)));
        }
        return result;
    }

    public static async Task<IReadOnlySet<uint>> LoadSpawnedCreatureIdsAsync(
        string connectionString,
        IEnumerable<uint> creatureIds,
        CancellationToken cancellationToken)
    {
        uint[] ids = creatureIds.Where(id => id != 0u).Distinct().ToArray();
        if (ids.Length == 0)
            return new HashSet<uint>();

        await using var connection = new MySqlConnection(ForDatabase(connectionString, WorldDatabase));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        var parameterNames = new List<string>(ids.Length);
        for (int i = 0; i < ids.Length; i++)
        {
            string name = $"@c{i}";
            parameterNames.Add(name);
            command.Parameters.AddWithValue(name, ids[i]);
        }

        command.CommandText = $"SELECT DISTINCT creature FROM entity WHERE creature IN ({string.Join(',', parameterNames)})";
        var result = new HashSet<uint>();
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetFieldValue<uint>(0));
        return result;
    }

    private static string ForDatabase(string connectionString, string database)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString) { Database = database };
        return builder.ConnectionString;
    }
}
