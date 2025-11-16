// <copyright file="P_ParcelUpdateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// System responsible for updating Parcel and Cell data when a parcel is placed, moved, or deleted.
    /// </summary>
    public partial class P_ParcelUpdateSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers
        private ModificationBarrier2 m_ModificationBarrier2;
        private EntityCommandBuffer m_CommandBuffer;

        // Queries
        private EntityQuery m_ParcelQuery;

        // Systems & References
        private P_UISystem m_PlatterUISystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelUpdateSystem));
            m_Log.Debug($"OnCreate()");

            // Retrieve Systems
            m_ModificationBarrier2 = World.GetOrCreateSystemManaged<ModificationBarrier2>();
            m_PlatterUISystem = World.GetOrCreateSystemManaged<P_UISystem>();

            // Queries
            m_ParcelQuery = SystemAPI.QueryBuilder()
                .WithAllRW<Parcel, ParcelSubBlock>()
                .WithAll<ParcelComposition, Initialized, Transform>()
                .WithAny<Updated, Deleted>()
                .WithNone<ParcelPlaceholder>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_ParcelQuery);
        }

        /// <inheritdoc/>
        /// Todo convert to job for perf
        protected override void OnUpdate() {
            m_CommandBuffer = m_ModificationBarrier2.CreateCommandBuffer();
            var entities = m_ParcelQuery.ToEntityArray(Allocator.Temp);
            var currentDefaultAllowSpawn = PlatterMod.Instance.Settings.AllowSpawn;

            // Hardcoded block size minimums
            // This is needed because the game requires
            //   - blocks to be at least 2 wide to not be automatically set to occupied (even though LOTS can be 1 wide, like for townhomes)
            //   - blocks to be 6 deep so that the rendering / culling system does not get confused and not render cells on certain smaller parcel sizes
            const int minBlockDepth = 6;
            const int minBlockWidth  = 2;

            foreach (var parcelEntity in entities) {
                var subBlockBuffer = EntityManager.GetBuffer<ParcelSubBlock>(parcelEntity);

                // DELETE state
                if (EntityManager.HasComponent<Deleted>(parcelEntity)) {
                    m_Log.Debug($"OnUpdate() -- [DELETE] Deleting parcel {parcelEntity} with {subBlockBuffer.Length} subBlocks");

                    for (var j = 0; j < subBlockBuffer.Length; j++) {
                        var subBlockEntity = subBlockBuffer[j].m_SubBlock;
                        var cellBuffer     = EntityManager.GetBuffer<Cell>(subBlockEntity);

                        // Clear cell zoning before deleting
                        // This prevents shenanigans from the vanilla system that may try to re-zone underlying vanilla cells
                        for (var i = 0; i < cellBuffer.Length; i++) {
                            var cell = cellBuffer[i];
                            cell.m_Zone = ZoneType.None;
                            cellBuffer[i] = cell;
                        }

                        // Mark Blocks for deletion
                        m_CommandBuffer.AddComponent<Deleted>(subBlockEntity, default);
                    }

                    return;
                }

                // UPDATE State
                m_Log.Debug($"OnUpdate() -- Running UPDATE logic");

                // Retrieve components
                var parcel = EntityManager.GetComponentData<Parcel>(parcelEntity);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(parcelEntity);
                var parcelData = EntityManager.GetComponentData<ParcelData>(prefabRef);
                var parcelComposition = EntityManager.GetComponentData<ParcelComposition>(parcelEntity);
                var transform = EntityManager.GetComponentData<Transform>(parcelEntity);
                var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);

                // Store Zoneblock
                parcelComposition.m_ZoneBlockPrefab = parcelData.m_ZoneBlockPrefab;
                m_CommandBuffer.SetComponent<ParcelComposition>(parcelEntity, parcelComposition);

                // Retrieve zone data
                var blockPrefab = parcelComposition.m_ZoneBlockPrefab;
                if (!EntityManager.TryGetComponent<ZoneBlockData>(blockPrefab, out var zoneBlockData)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find ZoneBlockData");
                    return;
                }

                // Spawnable?
                if (currentDefaultAllowSpawn) {
                    m_CommandBuffer.AddComponent<ParcelSpawnable>(parcelEntity, default);
                }

                // If this is a temp entity, exit.
                if (EntityManager.HasComponent<Temp>(parcelEntity)) {
                    return;
                }

                // Zone Block Data
                var curvePosition = new CurvePosition() {
                    m_CurvePosition = new float2(1f, 0f),
                };

                var parcelBlockWidth = math.max(minBlockWidth, parcelData.m_LotSize.x);
                var parcelBlockDepth = math.max(minBlockDepth, parcelData.m_LotSize.y);

                var block = new Block() {
                    m_Position  = ParcelUtils.GetWorldPosition(transform, parcelGeo.BlockCenter),
                    m_Direction = math.mul(transform.m_Rotation, new float3(0f, 0f, 1f)).xz,
                    m_Size      = new int2(parcelBlockWidth, parcelBlockDepth),
                };

                // Build order is used for cell priority calculations, lowest = oldest = higher priority.
                // Manually setting to 0 ensures priority.
                var buildOrder = new BuildOrder() {
                    m_Order = 0,
                };

                // For now, we know there's only going to be one block per component
                if (subBlockBuffer.Length > 0) {
                    var subBlockEntity = subBlockBuffer[0].m_SubBlock;
                    m_CommandBuffer.SetComponent<PrefabRef>(subBlockEntity, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<Block>(subBlockEntity, block);
                    m_CommandBuffer.SetComponent<CurvePosition>(subBlockEntity, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(subBlockEntity, buildOrder);
                    m_CommandBuffer.AddComponent<Updated>(subBlockEntity, default);
                } else {
                    var blockEntity = m_CommandBuffer.CreateEntity(zoneBlockData.m_Archetype);
                    m_CommandBuffer.SetComponent<PrefabRef>(blockEntity, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<Block>(blockEntity, block);
                    m_CommandBuffer.SetComponent<CurvePosition>(blockEntity, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(blockEntity, buildOrder);
                    m_CommandBuffer.AddComponent<ParcelOwner>(blockEntity, new ParcelOwner(parcelEntity));

                    var cellBuffer = m_CommandBuffer.SetBuffer<Cell>(blockEntity);

                    // Set all cells outside of parcel bounds to occupied
                    for (var row = 0; row < block.m_Size.y; row++) {
                        for (var col = 0; col < block.m_Size.x; col++) {
                            var cell = new Cell();
                            if (col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y) {
                                // Set all cells beyond the depth or width limit to blocked
                                // This is due to a graphical limitation of the vanilla game that needs the depth to be 6 to fully render cells
                                // as well as the fact that a block has a minimum width of 2 to be valid.
                                cell.m_State = CellFlags.Blocked;
                            } else {
                                cell.m_Zone = parcel.m_PreZoneType;
                                // "Visible" flag is usually added by the game's cell overlap systems
                                // By adding it manually, we ensure that the game takes our custom blocks as a priority (given the 0 build order)
                                // otherwise it always prioritizes visible cells first, which would be the ones already existing on roads
                                // This may break in the future should the logic in CellOverlapJobs.cs change
                                cell.m_State = CellFlags.Visible;
                            }
                            cellBuffer.Add(cell);
                        }
                    }
                }
            }

            entities.Dispose();
        }
    }
}
