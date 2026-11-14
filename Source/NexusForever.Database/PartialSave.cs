using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NLog;

namespace NexusForever.Database
{
    /// <summary>
    /// Commit a change set built from dirty game state without throwing it away when the database rejects it.
    /// </summary>
    /// <remarks>
    /// Game objects clear their dirty flags while they build the change set, so once it is built the tracked entries are the only record of what is unsaved.
    /// Transient errors are already retried by the execution strategy (EnableRetryOnFailure). A rejection (duplicate key, an update or delete that
    /// affected 0 rows, a foreign key) used to roll back and lose the whole change set, so one bad row lost every change of that save.
    /// Here the rejected change set is committed again row by row: every row the database accepts is saved, only the rows it rejects are dropped and logged.
    /// Retrying the rejected rows later would not help, the database rejects them the same way every time.
    /// </remarks>
    public static class PartialSave
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Passes over the remaining rows, a row that depends on another one (a foreign key) can succeed on a later pass.
        /// </summary>
        private const int MaxPasses = 4;

        private sealed class PendingRow
        {
            public required object Entity { get; init; }
            public required EntityState State { get; init; }
            public required List<string> ModifiedProperties { get; init; }
            public required string Description { get; init; }
            public Exception LastError { get; set; }
        }

        /// <summary>
        /// Save the changes of <paramref name="context"/>, if the database rejects them commit them row by row in a context from <paramref name="contextFactory"/>.
        /// </summary>
        /// <remarks>
        /// The rows saved after a rejection are each committed on their own, so the change set is no longer atomic. Use this only for change sets of independent
        /// dirty state (a periodic player save), never where a partial result is wrong (creating a character).
        /// </remarks>
        public static async Task SaveChangesPartialAsync<T>(this T context, Func<T> contextFactory, string owner) where T : DbContext
        {
            try
            {
                await context.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException exception)
            {
                log.Error(exception, $"Save of {owner} was rejected by the database, committing its changes row by row so only the rejected rows are lost.");
            }

            List<PendingRow> pending = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => new PendingRow
                {
                    Entity             = e.Entity,
                    State              = e.State,
                    ModifiedProperties = e.Properties
                        .Where(p => p.IsModified)
                        .Select(p => p.Metadata.Name)
                        .ToList(),
                    Description        = Describe(e)
                })
                .ToList();
            context.ChangeTracker.Clear();

            int total = pending.Count;

            await using T salvage = contextFactory();
            // states are set explicitly per row, change detection must not pull related entities in through navigations
            salvage.ChangeTracker.AutoDetectChangesEnabled = false;

            bool progress = true;
            for (int pass = 0; pass < MaxPasses && progress && pending.Count > 0; pass++)
            {
                progress = false;
                foreach (PendingRow row in pending.ToList())
                {
                    salvage.ChangeTracker.Clear();
                    Track(salvage, row);

                    try
                    {
                        await salvage.SaveChangesAsync();
                        pending.Remove(row);
                        progress = true;
                    }
                    catch (DbUpdateConcurrencyException) when (row.State == EntityState.Deleted)
                    {
                        // the row is already gone, which is all the delete wanted
                        pending.Remove(row);
                        progress = true;
                    }
                    catch (DbUpdateException exception)
                    {
                        row.LastError = exception;
                    }
                }
            }

            salvage.ChangeTracker.Clear();

            foreach (PendingRow row in pending)
                log.Error($"Save of {owner} dropped {row.Description}: {row.LastError?.GetBaseException().Message}");

            log.Warn($"Save of {owner} recovered after a rejection: {total - pending.Count} of {total} rows committed, {pending.Count} dropped.");
        }

        private static void Track(DbContext context, PendingRow row)
        {
            // Entry().State tracks this one entity only, not the graph reachable from its navigations
            EntityEntry entry = context.Entry(row.Entity);
            switch (row.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Added;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Deleted;
                    break;
                case EntityState.Modified:
                    entry.State = EntityState.Unchanged;
                    foreach (string property in row.ModifiedProperties)
                        entry.Property(property).IsModified = true;
                    break;
            }
        }

        private static string Describe(EntityEntry entry)
        {
            IEnumerable<object> key = entry.Metadata.FindPrimaryKey()?.Properties
                .Select(p => entry.Property(p.Name).CurrentValue) ?? [];
            return $"{entry.State} {entry.Metadata.ClrType.Name} ({string.Join(", ", key)})";
        }
    }
}
