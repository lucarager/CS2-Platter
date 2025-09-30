// <copyright file="ParcelPrefab.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Prefabs {
    using Game.Zones;
    using Platter.Components;
    using System.Collections.Generic;
    using Unity.Entities;

    /// <summary>
    /// Todo.
    /// </summary>
    public class ParcelPrefab : StaticObjectPrefab {
        /// <summary>
        /// Todo.
        /// </summary>
        public int m_LotWidth = 2;

        /// <summary>
        /// Todo.
        /// </summary>
        public int m_LotDepth = 2;

        /// <summary>
        /// Todo.
        /// </summary>
        public ZoneBlockPrefab m_ZoneBlock;

        /// <summary>
        /// todo.
        /// </summary>
        public ZoneType m_PreZoneType;

        /// <summary>
        /// todo.
        /// </summary>
        public bool m_AllowSpawning;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelPrefab"/> class.
        /// </summary>
        public ParcelPrefab() {
        }

        /// <summary>
        /// Gets the parcel's LotSize.
        /// </summary>
        public int LotSize => m_LotWidth * m_LotDepth;

        /// <inheritdoc/>
        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);
            if (m_ZoneBlock != null) {
                prefabs.Add(m_ZoneBlock);
            }
        }

        /// <inheritdoc/>
        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            base.GetPrefabComponents(components);

            components.Add(ComponentType.ReadWrite<ParcelData>());
            components.Add(ComponentType.ReadWrite<PlaceableObjectData>());

            // Making it a "building" fixes snapping
            // The following are required for this to be a valid building
            // See BuildingInitializeSystem.onUpdate()
            components.Add(ComponentType.ReadWrite<BuildingData>());

            // components.Add(ComponentType.ReadWrite<ObjectGeometryData>());
            // components.Add(ComponentType.ReadWrite<BuildingTerraformData>());
            // components.Add(ComponentType.ReadWrite<BuildingTerraformOverride>());

            // Experimental
            components.Add(ComponentType.ReadWrite<ObjectSubAreas>());
        }

        /// <inheritdoc/>
        public override void GetArchetypeComponents(HashSet<ComponentType> components) {
            base.GetArchetypeComponents(components);

            components.Add(ComponentType.ReadWrite<Parcel>());
            components.Add(ComponentType.ReadWrite<ParcelComposition>());
            components.Add(ComponentType.ReadWrite<SubBlock>());
        }

        /// <inheritdoc/>
        public override void Initialize(EntityManager entityManager, Entity entity) {
            base.Initialize(entityManager, entity);

            // var m_Log = new PrefixedLogger("ParcelPrefab");
            // m_Log.Debug($"Initialize() {entity.GetHashCode()} - {m_LotWidth}{m_LotDepth}");

            //// Parceldata
            // var parcelData = entityManager.GetComponentData<ParcelData>(entity);
            // parcelData.m_LotSize = new int2(m_LotWidth, m_LotDepth);
            // entityManager.SetComponentData<ParcelData>(entity, parcelData);

            //// Some dimensions.
            // var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);

            // m_Log.Debug($"Initialize() {entity.GetHashCode()} - {parcelData.m_LotSize}{parcelGeo.Size} {parcelGeo.ToJSONString()}");

            //// Geometry data
            // var oGeoData = entityManager.GetComponentData<ObjectGeometryData>(entity);
            // oGeoData.m_MinLod = 100;
            // oGeoData.m_Size = parcelGeo.Size;
            // oGeoData.m_Pivot = parcelGeo.Pivot;
            // oGeoData.m_LegSize = new float3(1f, 1f, 1f);
            // oGeoData.m_LegOffset = new float2(1f, 1f);
            // oGeoData.m_Bounds = parcelGeo.Bounds;
            // oGeoData.m_Layers = MeshLayer.First;
            // oGeoData.m_Flags = GeometryFlags.WalkThrough;
            // entityManager.SetComponentData<ObjectGeometryData>(entity, oGeoData);

            // m_Log.Debug($"Initialize() {entity.GetHashCode()} - {oGeoData.ToJSONString()}");

            //// Placeable data
            // var placeableData = entityManager.GetComponentData<PlaceableObjectData>(entity);
            // placeableData.m_Flags |= Game.Objects.PlacementFlags.RoadSide | Game.Objects.PlacementFlags.SubNetSnap | Game.Objects.PlacementFlags.Floating;
            // placeableData.m_PlacementOffset = new float3(0, 0, 0); // Seems to only be used for "Shore" snapping
            // entityManager.SetComponentData<PlaceableObjectData>(entity, placeableData);

            // Building Data
            // var buildingData = entityManager.GetComponentData<BuildingData>(entity);
            // buildingData.m_LotSize = parcelData.m_LotSize;
            // buildingData.m_Flags = BuildingFlags.RequireRoad;
        }

        /// <inheritdoc/>
        // public override void LateInitialize(EntityManager entityManager, Entity currentEntity) {
        //    var prefabSystem = entityManager.World.GetExistingSystemManaged<PrefabSystem>();
        //    var prefabData = entityManager.GetComponentData<PrefabData>(currentEntity);
        //    var prefabBase = prefabSystem.GetPrefab<PrefabBase>(prefabData);
        //    var parcelPrefab = prefabBase.GetComponent<ParcelPrefab>();
        //    var zoneBlockPrefab = prefabSystem.GetEntity(parcelPrefab.m_ZoneBlock);

        // // Parceldata
        //    var parcelData = entityManager.GetComponentData<ParcelData>(currentEntity);
        //    parcelData.m_ZoneBlockPrefab = zoneBlockPrefab;
        //    parcelData.m_LotSize = new int2(parcelPrefab.m_LotWidth, parcelPrefab.m_LotDepth);
        //    entityManager.SetComponentData<ParcelData>(currentEntity, parcelData);

        // // Some dimensions.
        //    var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);

        // // Geometry data
        //    var oGeoData = entityManager.GetComponentData<ObjectGeometryData>(currentEntity);
        //    oGeoData.m_MinLod = 100;
        //    oGeoData.m_Size = parcelGeo.Size;
        //    oGeoData.m_Pivot = parcelGeo.Pivot;
        //    oGeoData.m_LegSize = new float3(0f, 0f, 0f);
        //    oGeoData.m_Bounds = parcelGeo.Bounds;
        //    oGeoData.m_Layers = MeshLayer.First;
        //    oGeoData.m_Flags &= ~GeometryFlags.Overridable;
        //    oGeoData.m_Flags |= GeometryFlags.Physical | GeometryFlags.WalkThrough;
        //    entityManager.SetComponentData<ObjectGeometryData>(currentEntity, oGeoData);

        // // Placeable data
        //    var placeableData = entityManager.GetComponentData<PlaceableObjectData>(currentEntity);
        //    placeableData.m_Flags |= Game.Objects.PlacementFlags.RoadEdge | Game.Objects.PlacementFlags.SubNetSnap | Game.Objects.PlacementFlags.OnGround;
        //    placeableData.m_PlacementOffset = new float3(100f, 0, 100f);
        //    entityManager.SetComponentData<PlaceableObjectData>(currentEntity, placeableData);
        // }
    }
}
