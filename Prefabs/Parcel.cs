using System;
using System.Collections.Generic;
using Game.Prefabs;
using Game.Zones;
using Unity.Entities;
using Unity.Mathematics;

namespace Platter.Prefabs {
    [ComponentMenu("Areas/", new Type[] { typeof(SpacePrefab) })]
    public class Parcel : ComponentBase {
        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);
            if (this.m_ZoneBlock != null) {
                prefabs.Add(this.m_ZoneBlock);
            }
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            components.Add(ComponentType.ReadWrite<ParcelData>());
        }

        public override void GetArchetypeComponents(HashSet<ComponentType> components) {
            if (this.m_ZoneBlock != null) {
                components.Add(ComponentType.ReadWrite<SubBlock>());
                return;
            }
        }

        public Parcel() {
        }

        public float3 m_Position;
        public float2 m_Direction;
        public int2 m_Size;
        public ZoneBlockPrefab m_ZoneBlock;
    }
}
