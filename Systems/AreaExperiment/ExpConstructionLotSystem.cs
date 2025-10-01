// <copyright file="ParcelAreaSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Entities;
    using Game;
    using Game.Areas;
    using Game.Buildings;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.UniversalDelegates;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ExpConstructionLotSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Barriers & Buffers
        private bool done = false;

        // Queries
        private EntityQuery m_Query;
        private EntityQuery m_ParcelQuery;

        // Systems & References
        private Game.Prefabs.PrefabSystem m_PrefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ExpConstructionLotSystem));
            m_Log.Debug($"OnCreate()");

            // Retrieve Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();

            // Queries
            m_Query = GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<UnderConstruction>(),
                ComponentType.Exclude<Destroyed>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            });

            m_ParcelQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>()
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Temp>()
                    },
            });

            // Update Cycle
            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities = m_Query.ToEntityArray(Allocator.Temp);
            var parcels = m_ParcelQuery.ToEntityArray(Allocator.Temp);

            if (parcels.Length == 0) {
                m_Log.Error($"OnUpdate() -- Parcel not found.");
                return;
            }

            var parcel = parcels[0];

            if (!EntityManager.TryGetBuffer<Game.Areas.SubArea>(parcel, false, out var parcelSubAreaBuffer)) {
                m_Log.Error($"OnUpdate() -- parcelSubAreaBuffer not found.");
                return;
            }

            var parcelSubArea = parcelSubAreaBuffer[0];

            if (!EntityManager.TryGetBuffer<Node>(parcelSubArea.m_Area, false, out var parcelAreaNodeBuffer)) {
                m_Log.Error($"OnUpdate() -- parcelAreaNodeBuffer not found.");
                return;
            }

            if (!EntityManager.TryGetBuffer<Expand>(parcelSubArea.m_Area, false, out var parcelAreaExpandBuffer)) {
                m_Log.Error($"OnUpdate() -- parcelAreaExpandBuffer not found.");
                return;
            }

            if (!EntityManager.TryGetBuffer<Triangle>(parcelSubArea.m_Area, false, out var parcelAreaTriangleBuffer)) {
                m_Log.Error($"OnUpdate() -- parcelAreaTriangleBuffer not found.");
                return;
            }

            foreach (var entity in entities) {
                if (EntityManager.HasBuffer<ConnectedParcel>(entity)) {
                    return;
                }

                var parcelBuffer = EntityManager.AddBuffer<ConnectedParcel>(entity);
                parcelBuffer.Add(new ConnectedParcel() {
                    m_Parcel = parcel
                });

                m_Log.Debug($"OnUpdate() -- Processing entity {entity} out of {entities.Length}");

                if (!EntityManager.TryGetBuffer<Game.Areas.SubArea>(entity, false, out var subAreaBuffer)) {
                    m_Log.Error($"OnUpdate() -- subAreaBuffer not found.");
                    return;
                }

                for (var i = 0; i < subAreaBuffer.Length; i++) {
                    var subArea = subAreaBuffer[i];

                    // Area
                    if (EntityManager.TryGetComponent<PrefabRef>(subArea.m_Area, out var prefabRef) &&
                        m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var subAreaPrefab)) {
                        m_Log.Debug($"OnUpdate() -- Building has the following Area: {subAreaPrefab.name}");

                        if (subAreaPrefab.name.StartsWith("Sand") &&
                            EntityManager.TryGetBuffer<Node>(subArea.m_Area, false, out var nodeBuffer) &&
                            EntityManager.TryGetBuffer<Expand>(subArea.m_Area, false, out var expandBuffer) &&
                            EntityManager.TryGetBuffer<Triangle>(subArea.m_Area, false, out var triangleBuffer)) {
                            nodeBuffer.Clear();
                            expandBuffer.Clear();
                            triangleBuffer.Clear();

                            for (var j = 0; j < parcelAreaNodeBuffer.Length; j++) {
                                nodeBuffer.Add(parcelAreaNodeBuffer[j]);
                            }

                            for (var j = 0; j < parcelAreaExpandBuffer.Length; j++) {
                                expandBuffer.Add(parcelAreaExpandBuffer[j]);
                            }

                            for (var j = 0; j < parcelAreaTriangleBuffer.Length; j++) {
                                triangleBuffer.Add(parcelAreaTriangleBuffer[j]);
                            }
                        }
                    }
                }
            }
        }
    }
}
