// <copyright file="P_ParcelInitializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for initializing ParcelPrefab data.
    /// This runs after ObjectInitializeSystem and manually sets things like geometry data.
    /// </summary>
    public partial class P_ParcelInitializeSystem : PlatterGameSystemBase {
        // Shared GeometryFlags for all parcels.
        private const GeometryFlags CommonGeometryFlags =
            // Ensures the parcel can be walked through.
            GeometryFlags.WalkThrough;

        // PermGeometryFlags: Assigned to permanent parcels (after placing)
        private const GeometryFlags PermGeometryFlags =
            CommonGeometryFlags |
            // Allows buildings and objects to grow over the parcel. However, we then remove the Overridden flag in another system.
            GeometryFlags.Overridable |
            // Reduces collision priority to avoid interference with other objects.
            GeometryFlags.LowCollisionPriority;

        // PlaceholderGeometryFlags: Assigned to placeholder parcels (before placing)
        private const GeometryFlags PlaceholderGeometryFlags =
            CommonGeometryFlags       |
            // Checks that no ground collision exists.
            GeometryFlags.ExclusiveGround |
            GeometryFlags.HasLot |
            // Necessary for ObjectTool to include linetool options.
            GeometryFlags.Brushable;

        // Shared PlacementFlags for all parcels.
        private const PlacementFlags CommonPlacementFlags =
            // Added to make sure EDT doesn't pick up parcels.
            PlacementFlags.OwnerSide;

        private EntityQuery    m_ParcelPlaceholderPrefabQuery;
        private EntityQuery    m_ParcelPrefabQuery;
        private PrefabSystem   m_PrefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Retrieve Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Queries
            m_ParcelPrefabQuery = SystemAPI.QueryBuilder()
                                           .WithAll<PrefabData, Created>()
                                           .WithAllRW<ParcelData>()
                                           .WithAllRW<ObjectGeometryData>()
                                           .WithAllRW<PlaceableObjectData>()
                                           .WithNone<ParcelPlaceholderData>()
                                           .Build();

            m_ParcelPlaceholderPrefabQuery = SystemAPI.QueryBuilder()
                                                      .WithAll<PrefabData, Created, ParcelPlaceholderData>()
                                                      .WithAllRW<ParcelData>()
                                                      .WithAllRW<ObjectGeometryData>()
                                                      .WithAllRW<PlaceableObjectData>()
                                                      .Build();

            // Update Cycle
            RequireForUpdate(m_ParcelPrefabQuery);
        }

        /// <inheritdoc/>
        // Todo convert to job for perf
        protected override void OnUpdate() {
            foreach (var prefabEntity in m_ParcelPrefabQuery.ToEntityArray(Allocator.Temp)) {
                InitializePrefab(prefabEntity);
            }

            foreach (var prefabEntity in m_ParcelPlaceholderPrefabQuery.ToEntityArray(Allocator.Temp)) {
                InitializePrefab(prefabEntity, true);
            }

            EntityManager.AddComponent<Updated>(m_ParcelPrefabQuery);
        }

        private void InitializePrefab(Entity prefabEntity, bool placeholder = false) {
            var prefabData = EntityManager.GetComponentData<PrefabData>(prefabEntity);

            // Get prefab data
            if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabData, out var prefabBase)) {
                return;
            }

            // Get Parcel ComponentBase
            int    lotWidth;
            int    lotDepth;
            Entity zoneBlockPrefab;

            if (placeholder) {
                var parcelPrefab = prefabBase.GetComponent<ParcelPlaceholderPrefab>();
                lotWidth        = parcelPrefab.m_LotWidth;
                lotDepth        = parcelPrefab.m_LotDepth;
                zoneBlockPrefab = m_PrefabSystem.GetEntity(parcelPrefab.m_ZoneBlock);
            } else {
                var parcelPrefab = prefabBase.GetComponent<ParcelPrefab>();
                lotWidth        = parcelPrefab.m_LotWidth;
                lotDepth        = parcelPrefab.m_LotDepth;
                zoneBlockPrefab = m_PrefabSystem.GetEntity(parcelPrefab.m_ZoneBlock);
            }

            // Parceldata
            var parcelData = EntityManager.GetComponentData<ParcelData>(prefabEntity);
            parcelData.m_ZoneBlockPrefab = zoneBlockPrefab;
            parcelData.m_LotSize         = new int2(lotWidth, lotDepth);
            EntityManager.SetComponentData(prefabEntity, parcelData);

            // Some dimensions.
            var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);

            // Geometry data
            var oGeoData = EntityManager.GetComponentData<ObjectGeometryData>(prefabEntity);
            oGeoData.m_MinLod  = 1;
            oGeoData.m_Size    = parcelGeo.Size;
            oGeoData.m_Pivot   = parcelGeo.Pivot;
            oGeoData.m_LegSize = new float3(0f, 0f, 0f);
            oGeoData.m_Bounds  = parcelGeo.Bounds;
            oGeoData.m_Layers  = MeshLayer.Default;
            oGeoData.m_Flags   = placeholder ? PlaceholderGeometryFlags : PermGeometryFlags;
            EntityManager.SetComponentData(prefabEntity, oGeoData);

            // Geometry data
            EntityManager.SetComponentData(
                prefabEntity,
                new PlaceableObjectData
                {
                    m_Flags            = CommonPlacementFlags,
                    m_PlacementOffset  = new float3(0, 0, 0),
                    m_ConstructionCost = 0,
                    m_XPReward         = 0,
                });

            m_Log.Debug($"OnUpdate() -- Initialized Parcel {parcelData.m_LotSize.x}x{parcelData.m_LotSize.y}. placeholder = {placeholder}");
        }
    }

    // Documentation for Geometry Flags:
    //None = 0,
	//Circular = 1,
	
	///// <summary>
	///// Marks object as allowing terrain/placement overrides. Objects with this flag can receive the Overridden component to hide them.
	///// Default flag for most placeable objects (trees, props, decorations).
	///// </summary>
	///// <remarks>
	///// Set for: Trees, props, decorations (any non-utility, non-net object in ObjectInitializeSystem).<br/>
	///// When overridden: Receives Overridden component tag which hides the object from rendering/gameplay.<br/>
	///// GroundHeightSystem: Objects with Overridable or Marker (but not DeleteOverridden) get terrain height updates.<br/>
	///// BoundsMask: Objects excluded from NotOverridden mask when Overridden component is added.<br/>
	///// Paired with: <see cref="Brushable"/> flag for paintable/editable objects.<br/>
	///// Related: <see cref="DeleteOverridden"/>, <see cref="Marker"/>, <see cref="Brushable"/>, Overridden component.
	///// </remarks>
	//Overridable = 2,
	
	//Marker = 4,
	
	///// <summary>
	///// Marks object as requiring exclusive ground access, preventing overlapping ground collisions.
	///// Automatically set for AssetStamps. Triggers lot rendering for buildings.
	///// </summary>
	///// <remarks>
	///// Set for: AssetStamps (auto), buildings with lots.<br/>
	///// Collision: CommonUtils.ExclusiveGroundCollision checks if both objects have OnGround and one has ExclusiveGround.<br/>
	///// Rendering: Triggers DrawLot in BuildingLotRenderSystem for buildings with this flag.<br/>
	///// LotHeightSystem: Roads/nets with CompositionState.ExclusiveGround checked against building lots.<br/>
	///// Related: <see cref="HasLot"/>, <see cref="Physical"/>, <see cref="OccupyZone"/>.
	///// </remarks>
	//ExclusiveGround = 8,
	
	///// <summary>
	///// Marks object as requiring deletion when terrain is modified or overridden by other objects.
	///// Prevents terrain height updates in GroundHeightSystem.
	///// </summary>
	///// <remarks>
	///// Set for: Objects that should be removed when terrain/placement changes.<br/>
	///// GroundHeightSystem: Skips terrain height updates for objects with this flag (checked alongside Overridable/Marker).<br/>
	///// BoundsMask: Excluded from NotOverridden mask for quad tree searches.<br/>
	///// Related: <see cref="Overridable"/>, terrain modification systems.
	///// </remarks>
	//DeleteOverridden = 16,
	
	///// <summary>
	///// Marks object as having solid physical collision. Objects block pathfinding and vehicle/creature movement.
	///// Set automatically when object has non-decal meshes. Opposite of WalkThrough.
	///// </summary>
	///// <remarks>
	///// Set for: Objects with solid meshes (determined by flag = MeshFlags.Decal check in InitializePrefab).<br/>
	///// Collision: VehicleCollisionIterator and CreatureCollisionIterator require BoundsMask.NotWalkThrough (Physical objects).<br/>
	///// BoundsMask: Physical objects excluded from NotWalkThrough mask, included in collision searches.<br/>
	///// Mutually exclusive with: <see cref="WalkThrough"/> (one or the other, never both).<br/>
	///// Related: <see cref="WalkThrough"/>, <see cref="LowCollisionPriority"/>.
	///// </remarks>
	//Physical = 32,
	
	///// <summary>
	///// Marks object as non-collidable, allowing vehicles/creatures to pass through without collision.
	///// Set automatically for objects without solid meshes, markers, asset stamps, and bridges.
	///// </summary>
	///// <remarks>
	///// Set for: Decal-only objects, MarkerObjects, AssetStamps, moveable bridges, pillars.<br/>
	///// Collision: VehicleCollisionIterator skips objects with WalkThrough flag in CheckCollision.<br/>
	///// BoundsMask: Objects with WalkThrough are included in NotWalkThrough mask (excluded from collision).<br/>
	///// Mutually exclusive with: <see cref="Physical"/> (one or the other, never both).<br/>
	///// Related: <see cref="Physical"/>, <see cref="Marker"/>, <see cref="ExclusiveGround"/>.
	///// </remarks>
	//WalkThrough = 64,
	
	//Standing = 128,
	//CircularLeg = 256,
	//OverrideZone = 512,
	//OccupyZone = 1024,
	//CanSubmerge = 2048,
	
	///// <summary>
	///// Enables base collision detection for elevated objects in zone occupation.
	///// Objects with BaseCollision set will block zone cells even when elevated.
	///// </summary>
	///// <remarks>
	///// Set for: Pillars (non-horizontal), bridge supports, vertical structural elements.<br/>
	///// In CellOccupyJobs: Prevents elevated flag when BaseCollision is set, causing zone cell occupation.<br/>
	///// Related: <see cref="IgnoreBottomCollision"/>, <see cref="HasBase"/>, <see cref="OccupyZone"/>.
	///// </remarks>
	//BaseCollision = 4096,
	
	//IgnoreSecondaryCollision = 8192,
	//OptionalAttach = 16384,
	//Brushable = 32768,
	//Stampable = 65536,
	//LowCollisionPriority = 131072,
	
	///// <summary>
	///// Ignores collision with the bottom face of object bounds during collision checks.
	///// Adjusts collision bounds by setting minimum Y to max(bounds.min.y, 0f).
	///// </summary>
	///// <remarks>
	///// Set for: Horizontal pillars, overhead structures, bridges.<br/>
	///// In CellOccupyJobs: Clips bottom bounds before zone collision tests.<br/>
	///// Related: <see cref="BaseCollision"/>, <see cref="Standing"/>.
	///// </remarks>
	//IgnoreBottomCollision = 262144,
	
	///// <summary>
	///// Indicates the object has base geometry that connects it to the ground.
	///// Automatically set when a mesh has MeshFlags.Base flag.
	///// </summary>
	///// <remarks>
	///// Set for: Buildings (always), AssetStamps, objects with BaseProperties.<br/>
	///// Triggers generation of additional base mesh geometry at runtime for ground integration.<br/>
	///// Camera collision: Destroyed buildings with HasLot + Physical skip collision tests.<br/>
	///// Related: <see cref="BaseCollision"/> for collision, <see cref="HasLot"/> for building lots.
	///// </remarks>
	//HasBase = 524288,
	
	///// <summary>
	///// Indicates the object has an associated building lot for placement and rendering.
	///// Set together with HasBase for Asset Stamps and building structures.
	///// </summary>
	///// <remarks>
	///// Set for: AssetStamps, Buildings, Extensions with lot definitions.<br/>
	///// Rendering: DrawLot called in BuildingLotRenderSystem when ExclusiveGround is set.<br/>
	///// Camera collision: Destroyed objects with HasLot + Physical skip collision.<br/>
	///// Related: <see cref="HasBase"/>, <see cref="ExclusiveGround"/>.
	///// </remarks>
	//HasLot = 1048576,
	
	//IgnoreLegCollision = 2097152
}