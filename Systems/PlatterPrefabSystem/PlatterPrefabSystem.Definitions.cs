// <copyright file="PlatterPrefabSystem.Definitions.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Prefabs;
    using Game.UI;
    using Platter.Utils;
    using System.Collections.Generic;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Todo.
    /// </summary>
    public partial class PlatterPrefabSystem : UISystemBase {
        /// <summary>
        /// Range of block sizes we support.
        /// <para>x = min width.</para>
        /// <para>y = min depth.</para>
        /// <para>z = max width.</para>
        /// <para>w = max width.</para>
        /// </summary>
        public static readonly int4 BlockSizes = new(2, 2, 6, 6);

        /// <summary>
        /// Todo.
        /// </summary>
        public static readonly string PrefabNamePrefix = "Parcel";

        /// <summary>
        /// Todo.
        /// </summary>
        public static readonly Dictionary<string, string[]> SourcePrefabNames = new() {
            { "subLanePrefab", new string[2] { "NetLaneGeometryPrefab", "EU Car Bay Line" } },
            { "roadPrefab", new string[2] { "RoadPrefab", "Alley" } },
            { "uiPrefab", new string[2] { "ZonePrefab", "EU Residential Mixed" } },
        };

        /// <summary>
        /// Todo.
        /// </summary>
        private static bool PrefabsAreInstalled;

        /// <summary>
        /// Cache for prefabs.
        /// </summary>
        private List<PrefabBase> m_PrefabBases;
        private Dictionary<PrefabBase, Entity> m_PrefabEntities;

        // Systems & References
        private static PrefabSystem m_PrefabSystem;
        private static World m_World;

        // Class State
        private readonly bool m_Executed = false;

        // Barriers & Buffers
        private readonly EndFrameBarrier m_Barrier;
        private readonly EndFrameBarrier m_EndFrameBarrier;
        private EntityCommandBuffer m_CommandBuffer;
        private EntityCommandBuffer m_BlockCommandBuffer;

        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_BuildingQuery;
        private EntityQuery m_UpdatedEdgesQuery;

        // Entities
        private Entity m_CachedBuildingEntity;
        private Entity m_CachedEdgeEntity;
        private EntityArchetype m_DefinitionArchetype;

        /// <summary>
        /// Data to define our prefabs.
        /// </summary>
        private struct CustomPrefabData {
            public int m_LotWidth;
            public int m_LotDepth;

            public CustomPrefabData(int w, int d) {
                m_LotWidth = w;
                m_LotDepth = d;
            }
        }
    }
}
