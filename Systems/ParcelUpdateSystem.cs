// <copyright file="ParcelUpdateSystem.cs" company="Luca Rager">
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
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ParcelUpdateSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers
        private ModificationBarrier4 m_ModificationBarrier;
        private EntityCommandBuffer m_CommandBuffer;

        // Queries
        private EntityQuery m_ParcelCreatedQuery;
        private EntityQuery m_ZoneQuery;

        // Systems & References
        private PrefabSystem m_PrefabSystem;
        private ZoneSystem m_ZoneSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ParcelUpdateSystem));
            m_Log.Debug($"OnCreate()");

            // Retriefve Systems
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier4>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ZoneSystem = World.GetOrCreateSystemManaged<ZoneSystem>();

            // Queries
            m_ParcelCreatedQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>()
                    },
                    Any = new ComponentType[] {
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Deleted>()
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>(), // todo handle temp entities
                    },
                });

            m_ZoneQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>()
                    },
                    Any = new ComponentType[] {
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Deleted>()
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>(),
                    },
                });

            // Update Cycle
            RequireForUpdate(m_ParcelCreatedQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            NativeArray<Entity> entities = m_ParcelCreatedQuery.ToEntityArray(Allocator.Temp);

            new Dictionary<Block, Entity>(32);

            m_Log.Debug($"OnUpdate() -- Found {entities.Length}");

            for (int i = 0; i < entities.Length; i++) {
                var parcelEntity = entities[i];

                if (!EntityManager.TryGetBuffer<SubBlock>(parcelEntity, false, out DynamicBuffer<SubBlock> subBlockBuffer)) {
                    return;
                }

                // DELETE state
                if (EntityManager.HasComponent<Deleted>(parcelEntity)) {
                    m_Log.Debug($"OnUpdate() -- [DELETE] Deleting parcel {parcelEntity}");

                    // Mark Blocks for deletion
                    for (int j = 0; j < subBlockBuffer.Length; j++) {
                        Entity subBlockEntity = subBlockBuffer[j].m_SubBlock;
                        this.m_CommandBuffer.AddComponent<Deleted>(subBlockEntity, default);
                    }

                    return;
                }

                // UPDATE State
                m_Log.Debug($"OnUpdate() -- Running UPDATE logic");

                // Retrieve components
                if (!EntityManager.TryGetComponent<Parcel>(parcelEntity, out var parcel) ||
                    !EntityManager.TryGetComponent<PrefabRef>(parcelEntity, out var prefabRef) ||
                    !m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var prefabBase) ||
                    !EntityManager.TryGetComponent<ParcelData>(prefabRef, out var parcelData) ||
                    !EntityManager.TryGetComponent<ParcelComposition>(parcelEntity, out var parcelComposition) ||
                    !EntityManager.TryGetComponent<Transform>(parcelEntity, out var transform)) {
                    m_Log.Error($"OnUpdate() -- Couldn't find all required components");
                    return;
                }

                var parcelPrefab = prefabBase.GetComponent<ParcelPrefab>();
                var parcelGeo = new ParcelGeometry(parcelData.m_LotSize);

                // Store Zoneblock
                parcelComposition.m_ZoneBlockPrefab = parcelData.m_ZoneBlockPrefab;
                m_CommandBuffer.SetComponent<ParcelComposition>(parcelEntity, parcelComposition);

                // Retrive zone data
                Entity blockPrefab = parcelComposition.m_ZoneBlockPrefab;
                if (!EntityManager.TryGetComponent<ZoneBlockData>(blockPrefab, out ZoneBlockData zoneBlockData)) {
                    return;
                }

                // Spawnable Data
                // Todo this should come from the tool.
                m_CommandBuffer.AddComponent<ParcelSpawnable>(parcelEntity, default);

                // Todo this should come from the tool of course.
                if (!m_PrefabSystem.TryGetPrefab(new PrefabID("ZonePrefab", "Commercial High"), out var zonePrefab) ||
                    !m_PrefabSystem.TryGetEntity(zonePrefab, out var zonePrefabEntity) ||
                    !EntityManager.TryGetComponent<ZoneData>(zonePrefabEntity, out ZoneData zoneData)) {
                    m_Log.Error("couldn't find zone");
                    return;
                }

                // Update prezoned type
                parcel.m_PreZoneType = zoneData.m_ZoneType;
                m_CommandBuffer.SetComponent<Parcel>(parcelEntity, parcel);

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
                    m_Size = new int2(parcelPrefab.m_LotWidth, parcelPrefab.m_LotDepth),
                };
                var buildOder = new BuildOrder() {
                    m_Order = 0,
                };

                // For now, we know there's only going to be one block per component
                if (subBlockBuffer.Length > 0) {
                    m_Log.Debug($"OnUpdate() -- Updating the old block...");
                    Entity subBlockEntity = subBlockBuffer[0].m_SubBlock;
                    m_CommandBuffer.SetComponent<PrefabRef>(subBlockEntity, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<Block>(subBlockEntity, block);
                    m_CommandBuffer.SetComponent<CurvePosition>(subBlockEntity, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(subBlockEntity, buildOder);
                    m_CommandBuffer.AddComponent<Updated>(subBlockEntity, default);
                } else {
                    m_Log.Debug($"OnUpdate() -- Creating a new block...");
                    Entity blockEntity = m_CommandBuffer.CreateEntity(zoneBlockData.m_Archetype);
                    m_CommandBuffer.SetComponent<PrefabRef>(blockEntity, new PrefabRef(blockPrefab));
                    m_CommandBuffer.SetComponent<Block>(blockEntity, block);
                    m_CommandBuffer.SetComponent<CurvePosition>(blockEntity, curvePosition);
                    m_CommandBuffer.SetComponent<BuildOrder>(blockEntity, buildOder);
                    m_CommandBuffer.AddComponent<ParcelOwner>(blockEntity, new ParcelOwner(parcelEntity));

                    DynamicBuffer<Cell> cellBuffer = m_CommandBuffer.SetBuffer<Cell>(blockEntity);

                    var cellCount = block.m_Size.x * block.m_Size.y;

                    for (int l = 0; l < cellCount; l++) {
                        cellBuffer.Add(new Cell() {
                            m_Zone = zoneData.m_ZoneType
                        });
                    }
                }
            }
        }
    }
}
