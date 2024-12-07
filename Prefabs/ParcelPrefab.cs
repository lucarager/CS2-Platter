using Game.Zones;
using Platter.Prefabs;
using System.Collections.Generic;
using Unity.Entities;

namespace Game.Prefabs {
    public class ParcelPrefab : StaticObjectPrefab {
        public int lotSize {
            get {
                return this.m_LotWidth * this.m_LotDepth;
            }
        }

        /// <inheritdoc/>
        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);
            if (this.m_ZoneBlock != null) {
                prefabs.Add(this.m_ZoneBlock);
            }
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            base.GetPrefabComponents(components);
            //components.Add(ComponentType.ReadWrite<BuildingData>());
            components.Add(ComponentType.ReadWrite<PlaceableObjectData>());
            components.Add(ComponentType.ReadWrite<ParcelData>());
            components.Add(ComponentType.ReadWrite<BuildingTerraformData>());
            //components.Add(ComponentType.ReadWrite<Effect>());
        }

        public override void GetArchetypeComponents(HashSet<ComponentType> components) {
            base.GetArchetypeComponents(components);
            components.Add(ComponentType.ReadWrite<Parcel>());
            components.Add(ComponentType.ReadWrite<ParcelComposition>());
            components.Add(ComponentType.ReadWrite<SubBlock>());
            components.Add(ComponentType.ReadWrite<SubLane>());
            components.Add(ComponentType.ReadWrite<SubObject>());
        }

        public ParcelPrefab() {
        }

        public int m_LotWidth = 2;

        public int m_LotDepth = 2;

        public ZoneBlockPrefab m_ZoneBlock;

        public Entity m_RoadEdge;
    }
}
