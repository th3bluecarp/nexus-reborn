using MySqlConnector;
using NexusForever.Cryptography;
using NexusForever.GameTable.Model;

namespace NexusForever.Bot;

/// <summary>
/// A world spawn; <paramref name="Health"/> is the entity_stats health (stat 0) when the loader read it, else 0.
/// </summary>
internal sealed record Spawn(uint Id, uint CreatureId, ushort WorldId, float X, float Y, float Z, uint Health = 0u)
{
    public double DistanceSquared(float x, float y, float z)
    {
        double dx = X - x;
        double dy = Y - y;
        double dz = Z - z;
        return dx * dx + dy * dy + dz * dz;
    }
}

internal sealed record ItemDropSources(
    uint ItemId,
    IReadOnlyList<uint> CreatureIds,
    IReadOnlyList<uint> ContainerItemIds);

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

        // the spawn's health (entity_stats stat 0) sizes the kill budget: an Augmented Slateform Slammer has 3870 at level 13
        // against about 1500 for an ordinary quest creature, and the bot needs 45-58 s per kill on it
        command.CommandText = $"SELECT e.id, e.creature, e.world, e.x, e.y, e.z, COALESCE(s.value, 0) FROM entity e LEFT JOIN entity_stats s ON s.id = e.id AND s.stat = 0 WHERE e.creature IN ({string.Join(',', parameterNames)}) ORDER BY e.world, e.creature, e.id";
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
                reader.GetFloat(5),
                (uint)Math.Max(0d, Convert.ToDouble(reader.GetValue(6)))));
        }
        return result;
    }

    public static async Task<IReadOnlyList<Spawn>> LoadSpawnsNearLocationsAsync(
        string connectionString,
        IEnumerable<WorldLocation2Entry> locations,
        float minimumRadius,
        CancellationToken cancellationToken)
    {
        WorldLocation2Entry[] points = locations
            .GroupBy(location => location.Id)
            .Select(group => group.First())
            .ToArray();
        if (points.Length == 0)
            return [];

        await using var connection = new MySqlConnection(ForDatabase(connectionString, WorldDatabase));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var clauses = new List<string>(points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            WorldLocation2Entry point = points[i];
            float radius = Math.Max(point.Radius, minimumRadius);
            clauses.Add($"(world = @world{i} AND x BETWEEN @minX{i} AND @maxX{i} AND y BETWEEN @minY{i} AND @maxY{i} AND z BETWEEN @minZ{i} AND @maxZ{i})");
            command.Parameters.AddWithValue($"@world{i}", point.WorldId);
            command.Parameters.AddWithValue($"@minX{i}", point.Position0 - radius);
            command.Parameters.AddWithValue($"@maxX{i}", point.Position0 + radius);
            command.Parameters.AddWithValue($"@minY{i}", point.Position1 - radius);
            command.Parameters.AddWithValue($"@maxY{i}", point.Position1 + radius);
            command.Parameters.AddWithValue($"@minZ{i}", point.Position2 - radius);
            command.Parameters.AddWithValue($"@maxZ{i}", point.Position2 + radius);
        }

        command.CommandText = $"SELECT id, creature, world, x, y, z FROM entity WHERE creature <> 0 AND ({string.Join(" OR ", clauses)}) ORDER BY world, creature, id";
        var result = new List<Spawn>();
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var spawn = new Spawn(
                reader.GetFieldValue<uint>(0),
                reader.GetFieldValue<uint>(1),
                reader.GetFieldValue<ushort>(2),
                reader.GetFloat(3),
                reader.GetFloat(4),
                reader.GetFloat(5));
            bool inRange = points.Any(point => point.WorldId == spawn.WorldId
                && spawn.DistanceSquared(point.Position0, point.Position1, point.Position2)
                    <= Math.Pow(Math.Max(point.Radius, minimumRadius), 2d));
            if (inRange)
                result.Add(spawn);
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

    public static async Task<IReadOnlyDictionary<uint, ItemDropSources>> LoadItemDropSourcesAsync(
        string connectionString,
        IEnumerable<uint> itemIds,
        CancellationToken cancellationToken)
    {
        uint[] ids = itemIds.Where(id => id != 0u).Distinct().Order().ToArray();
        var result = new Dictionary<uint, ItemDropSources>();
        if (ids.Length == 0)
            return result;

        await using var connection = new MySqlConnection(ForDatabase(connectionString, WorldDatabase));
        await connection.OpenAsync(cancellationToken);

        foreach (uint itemId in ids)
        {
            uint[] creatureIds = await LoadLootOwnersAsync(connection, "entity_loot", itemId, cancellationToken);
            uint[] containerItemIds = await LoadLootOwnersAsync(connection, "item_loot", itemId, cancellationToken);
            result[itemId] = new ItemDropSources(itemId, creatureIds, containerItemIds);
        }
        return result;
    }

    private static async Task<uint[]> LoadLootOwnersAsync(
        MySqlConnection connection,
        string ownerTable,
        uint itemId,
        CancellationToken cancellationToken)
    {
        // LootGroupManager attaches entity_loot/item_loot only to root groups. Walk from every
        // group containing the static item through its parents so nested group items resolve to
        // the same owner the server loads.
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            WITH RECURSIVE item_groups (id) AS
            (
                SELECT li.id
                FROM loot_item li
                WHERE li.type = 0 AND li.staticId = @itemId
                UNION DISTINCT
                SELECT lg.parentId
                FROM loot_group lg
                INNER JOIN item_groups child ON child.id = lg.id
                WHERE lg.parentId IS NOT NULL
            )
            SELECT DISTINCT owner.id
            FROM {ownerTable} owner
            INNER JOIN item_groups groups_with_item ON groups_with_item.id = owner.lootGroupId
            ORDER BY owner.id
            """;
        command.Parameters.AddWithValue("@itemId", itemId);

        var owners = new List<uint>();
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            owners.Add(reader.GetFieldValue<uint>(0));
        return owners.ToArray();
    }

    private static string ForDatabase(string connectionString, string database)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString) { Database = database };
        return builder.ConnectionString;
    }
}
