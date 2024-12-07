namespace Platter.Prefabs {
    using Unity.Entities;

    public struct ParcelComposition : IComponentData, IQueryTypeParameter {
        public Entity m_ZoneBlockPrefab;
    }
}
