using System;
using System.Collections.Generic;
using Colossal.Json;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Effects;
using Game.Net;
using Game.Objects;
using Game.Policies;
using Game.Simulation;
using Game.UI.Editor;
using Game.UI.Widgets;
using Game.Zones;
using Platter;
using Platter.Prefabs;
using Unity.Entities;
using Unity.Mathematics;

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
            //components.Add(ComponentType.ReadWrite<BuildingTerraformData>());
            //components.Add(ComponentType.ReadWrite<Effect>());
        }

        public override void GetArchetypeComponents(HashSet<ComponentType> components) {
            base.GetArchetypeComponents(components);
            components.Add(ComponentType.ReadWrite<Parcel>());
            components.Add(ComponentType.ReadWrite<ParcelComposition>());
            components.Add(ComponentType.ReadWrite<SubBlock>());
            components.Add(ComponentType.ReadWrite<SubNet>());
        }

        public ParcelPrefab() {
        }

        [CustomField(typeof(BuildingLotWidthField))]
        public int m_LotWidth = 4;

        [CustomField(typeof(BuildingLotDepthField))]
        public int m_LotDepth = 2;

        public ZoneBlockPrefab m_ZoneBlock;

        public Entity m_RoadEdge;

        public static float m_CellSize = 8f;
    }
}
