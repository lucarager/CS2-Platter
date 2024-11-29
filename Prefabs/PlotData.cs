namespace Platter.Prefabs {
    using System;
    using Game.Objects;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct PlotData : IComponentData, IQueryTypeParameter {
        public float2 m_ForwardDirection;
        public int2 m_PlotSize;
        public Transform m_PlotTransform;
    }
}
