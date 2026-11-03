using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Event;
using NexusForever.Game.Prerequisite;
using NexusForever.Game.Spell.Event;
using NexusForever.Game.Static.Spell;

namespace NexusForever.Game.Spell
{
    public class Proxy : IProxy
    {
        public IUnitEntity Target { get; }
        public ISpellEffectProxyData Data { get; }
        public ISpell ParentSpell { get; }
        public bool CanCast { get; private set; } = false;

        private ISpellParameters proxyParameters;
        private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, byte> missingProxySpellIds = new();
        private static readonly MissingSet missingProxySpellsLogged = new();

        private sealed class MissingSet
        {
            public bool Add(uint id) => missingProxySpellIds.TryAdd(id, 0);
        }

        public Proxy(IUnitEntity target, ISpellEffectProxyData data, ISpell parentSpell, ISpellParameters parameters)
        {
            Target = target;
            Data = data;
            ParentSpell = parentSpell;

            proxyParameters = new SpellParameters
            {
                ParentSpellInfo = parameters.SpellInfo,
                RootSpellInfo = parameters.RootSpellInfo,
                PrimaryTargetId = Target.Guid,
                UserInitiatedSpellCast = parameters.UserInitiatedSpellCast,
                IsProxy = true
            };
        }

        public void Evaluate()
        {
            if (Target is not IPlayer)
                CanCast = true;

            if (Data.PrerequisiteId == 0)
                CanCast = true;

            if (CanCast)
                return;

            if (PrerequisiteManager.Instance.Meets(Target as IPlayer, Data.PrerequisiteId))
                CanCast = true;
        }

        public void Cast(IUnitEntity caster, ISpellEventManager events)
        {
            if (!CanCast)
                return;

            // a proxy naming a spell that is not in our Spell4 table (a Shoveljaw Boulderback's aura in Algoroc) threw out of
            // the creature's update every tick; it is skipped instead
            uint castId = ParentSpell.CastMethod == CastMethod.Aura && Data.Entry.TickTime > 0 || Data.Entry.TickTime > 0
                ? Data.PeriodicSpellId
                : Data.SpellId;
            if (NexusForever.GameTable.GameTableManager.Instance.Spell4.GetEntry(castId) == null)
            {
                if (missingProxySpellsLogged.Add(castId))
                    log.Warn($"Proxy of spell {ParentSpell.Spell4Id} names spell {castId}, which is not in the Spell4 table; skipped.");
                return;
            }

            if (ParentSpell.CastMethod == CastMethod.Aura && Data.Entry.TickTime > 0)
            {
                caster.CastSpell(Data.PeriodicSpellId, proxyParameters);
                return;
            }

            if (Data.Entry.TickTime > 0)
            {
                double tickTime = Data.Entry.TickTime;
                if (Data.Entry.DurationTime > 0)
                {
                    for (int i = 1; i >= Data.Entry.DurationTime / tickTime; i++)
                    {
                        events.EnqueueEvent(new SpellEvent(tickTime * i / 1000d, () =>
                        {
                            caster.CastSpell(Data.PeriodicSpellId, proxyParameters);
                        }));
                    }
                }
                else
                    events.EnqueueEvent(TickingEvent(tickTime, () =>
                    {
                        caster.CastSpell(Data.PeriodicSpellId, proxyParameters);
                    }));
            }
            else
                caster.CastSpell(Data.SpellId, proxyParameters);
        }

        private SpellEvent TickingEvent(double tickTime, Action action)
        {
            return new SpellEvent(tickTime / 1000d, () =>
            {
                action.Invoke();
                TickingEvent(tickTime, action);
            });
        }
    }
}