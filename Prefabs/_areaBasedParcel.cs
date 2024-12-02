namespace Platter.Prefabs {
    using System;
    using System.Collections.Generic;
    using Game.Areas;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Zones;
    using Unity.Entities;
    using Unity.Mathematics;

    [ComponentMenu("Areas/", new Type[] { typeof(SpacePrefab) })]
    public class _areaBasedParcel : ComponentBase {
        /// <inheritdoc/>
        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);
            if (this.m_ZoneBlock != null) {
                prefabs.Add(this.m_ZoneBlock);
            }
        }

        /// <inheritdoc/>
        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            components.Add(ComponentType.ReadWrite<ParcelData>());
            components.Add(ComponentType.ReadWrite<PlotData>());
        }

        /// <inheritdoc/>
        public override void GetArchetypeComponents(HashSet<ComponentType> components) {
            components.Add(ComponentType.ReadWrite<Parcel>());
            components.Add(ComponentType.ReadWrite<ParcelComposition>());
            components.Add(ComponentType.ReadWrite<SubBlock>());
        }
        /// <inheritdoc/>
        public override void LateInitialize(EntityManager entityManager, Entity entity) {
            base.LateInitialize(entityManager, entity);
            PlotData plotData;
            plotData.m_ForwardDirection = new float2(0.8085141f, 0.588477f);
            plotData.m_PlotSize = new int2(2, 2);
            plotData.m_PlotTransform = default(Transform);
            entityManager.SetComponentData<PlotData>(entity, plotData);
        }

        public _areaBasedParcel() {
        }

        public ZoneBlockPrefab m_ZoneBlock;
        public Entity m_RoadEdge;
        public float2 m_ForwardDirection;
        public int2 m_PlotSize;
        public Transform m_PlotTransform;
    }
}
