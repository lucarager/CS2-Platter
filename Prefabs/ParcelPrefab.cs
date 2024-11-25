using System;
using System.Collections.Generic;
using Game.Areas;
using Game.Prefabs;
using Game.Zones;
using Platter.Components;
using Unity.Entities;
using UnityEngine;

namespace Platter.Prefabs {
    [ComponentMenu("Zones/", new Type[] { })]
    public class ParcelPrefab : AreaPrefab {
        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);
            prefabs.Add(this.m_ZoneBlock);
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            base.GetPrefabComponents(components);
            // Our own ingredients
            components.Add(ComponentType.ReadWrite<ParcelData>());
            // Copied from "districts"
            components.Add(ComponentType.ReadWrite<AreaNameData>());
        }

        public override void GetArchetypeComponents(HashSet<ComponentType> components) {
            base.GetArchetypeComponents(components);
            // Our own ingredients
            components.Add(ComponentType.ReadWrite<Parcel>());
            components.Add(ComponentType.ReadWrite<Block>());
            // Copied from "districts" 
            components.Add(ComponentType.ReadWrite<LabelExtents>());
            components.Add(ComponentType.ReadWrite<LabelVertex>());
        }

        public override void Initialize(EntityManager entityManager, Entity entity) {
            base.Initialize(entityManager, entity);
            // Copied from "districts" 
            entityManager.SetComponentData<AreaNameData>(entity, new AreaNameData {
                m_Color = this.m_NameColor,
                m_SelectedColor = this.m_SelectedNameColor
            });
        }

        public ParcelPrefab() {
        }

        public float m_MaxRadius = 200f;

        public global::UnityEngine.Color m_RangeColor = global::UnityEngine.Color.white;
        
        public Color m_NameColor = Color.white;

        public Color m_SelectedNameColor = new Color(0.5f, 0.75f, 1f, 1f);

        public ZoneBlockPrefab m_ZoneBlock;
    }
}
