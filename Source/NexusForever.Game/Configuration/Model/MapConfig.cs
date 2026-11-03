using NexusForever.Shared.Configuration;

namespace NexusForever.Game.Configuration.Model
{
    [ConfigurationBind]
    public class MapConfig
    {
        public string MapPath { get; set; } = "map";
        public List<ushort> PrecacheBaseMaps { get; set; }
        public List<ushort> PrecacheMapSpawns { get; set; }
        public bool SynchronousUpdate { get; set; } = false;
        public uint? GridActionThreshold { get; set; } = 2000u; // was 100: a crowded zone enqueues more relocates per tick than that and player teleports starved behind them (Algoroc 2026-11-02)
        public uint? GridActionMaxRetry { get; set; } = 5u;
        public double? GridActionBudgetMs { get; set; } = 25d; // movement relocates beyond the threshold run until this much of the tick is spent
        public double? GridUnloadTimer { get; set; } = 600u;
        public uint? MaxInstances { get; set; } = 10u;
    }
}