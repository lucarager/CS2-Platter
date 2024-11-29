namespace Platter.Prefabs {
    using System;
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    public struct ParcelComposition : IComponentData, IQueryTypeParameter {
        public Entity m_ZoneBlockPrefab;
    }
}
