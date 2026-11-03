using System.Numerics;

namespace NexusForever.Game.Abstract.Map
{
    public interface IGridActionRelocate : IGridAction
    {
        Vector3 Vector { get; init; }
        OnRelocateDelegate Callback { get; init; }

        /// <summary>
        /// A plain movement step: when a newer relocate of the same entity is queued, this one is superseded and skipped.
        /// </summary>
        bool Movement { get; init; }
    }
}