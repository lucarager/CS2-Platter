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
        private ModificationBarrier4 m_ModificationBarrier;
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
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier4>();
            m_PlatterUISystem = World.GetOrCreateSystemManaged<P_UISystem>();

            // Queries
            m_ParcelQuery = SystemAPI.QueryBuilder()
                .WithAllRW<Parcel, ParcelSubBlock>()
                .WithAll<PrefabRef, ParcelComposition, Transform>()
                .WithAny<Updated, Deleted>()
                .WithNone<Temp>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_ParcelQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            var entities = m_ParcelQuery.ToEntityArray(Allocator.Temp);
            var currentDefaultAllowSpawn = m_PlatterUISystem.AllowSpawning;

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

                // Retrive zone data
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
                var block = new Block() {
                    m_Position = ParcelUtils.GetWorldPosition(transform, parcelGeo.Center),
                    m_Direction = math.mul(transform.m_Rotation, new float3(0f, 0f, 1f)).xz,
                    m_Size = new int2(parcelData.m_LotSize.x, parcelData.m_LotSize.y),
                };

                // Builorder is used for cell priority calculations, lowest = oldest = higher priority.
                // Manually setting to 0 ensures priority.
                var buildOder = new BuildOrder() {
                    m_Order = 0,
                };

                // For now, we know there's only going to be one block per component
                if (subBlockBuffer.Length > 0) {
                    var subBlockEntity = subBlockBuffer[0].m_SubBlock;
                    m_CommandBuffer.SetComponent<PrefabRef>(subBlockEntity, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<Block>(subBlockEntity, block);
                    m_CommandBuffer.SetComponent<CurvePosition>(subBlockEntity, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(subBlockEntity, buildOder);
                    m_CommandBuffer.AddComponent<Updated>(subBlockEntity, default);
                } else {
                    var blockEntity = m_CommandBuffer.CreateEntity(zoneBlockData.m_Archetype);
                    m_CommandBuffer.SetComponent<PrefabRef>(blockEntity, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<Block>(blockEntity, block);
                    m_CommandBuffer.SetComponent<CurvePosition>(blockEntity, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(blockEntity, buildOder);
                    m_CommandBuffer.AddComponent<ParcelOwner>(blockEntity, new ParcelOwner(parcelEntity));

                    var cellBuffer = m_CommandBuffer.SetBuffer<Cell>(blockEntity);

                    var cellCount = block.m_Size.x * block.m_Size.y;

                    for (var l = 0; l < cellCount; l++) {
                        cellBuffer.Add(new Cell() {
                            m_Zone = parcel.m_PreZoneType,

                            // "Visible" flag is usually added by the game's cell overlap systems
                            // By adding it manually, we ensure that the game takes our custom blocks as a priority (given the 0 build order)
                            // otherwise it always prioritizes visible cells first, which would be the ones already existing on roads
                            // This may break in the future should the logic in CellOverlapJobs.cs change
                            m_State = CellFlags.Visible,
                        });
                    }
                }
            }

            entities.Dispose();
        }
    }
}
