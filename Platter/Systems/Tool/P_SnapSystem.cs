// <copyright file="P_SnapSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using Colossal.Entities;
    using Colossal.Mathematics;
    using Components;
    using Extensions;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Utils;
    using Block = Game.Zones.Block;
    using SearchSystem = Game.Net.SearchSystem;
    using Transform = Game.Objects.Transform;

    #endregion

    /// <summary>
    /// Ovverides object placement to snap parcels to road sides 
    /// </summary>
    public partial class P_SnapSystem : PlatterGameSystemBase {
        [Flags]
        public enum SnapMode : uint {
            None             = 0,
            ZoneSide         = 1,
            RoadSide         = 2,
            ParcelEdge       = 4,
            ParcelFrontAlign = 8,
        }

        private static class SnapLevel {
            public const float None             = 0f;
            public const float ParcelEdge       = 1.5f;
            public const float RoadSide         = 2f;
            public const float ParcelFrontAlign = 2.5f;
            public const float ZoneSide         = 2.5f;
        }

        private EntityQuery m_Query;
        private float       m_SnapSetback;
        public  NativeReference<bool>   IsSnapped;
        private ObjectToolSystem        m_ObjectToolSystem;
        private SnapMode                m_SnapMode;

        public float CurrentSnapSetback {
            get => m_SnapSetback;
            set {
                m_SnapSetback = value;
                m_ObjectToolSystem.SetMemberValue("m_ForceUpdate", true);
            }
        }

        public static float DefaultSnapDistance => MinSnapDistance;
        public static float MaxSnapDistance     => 8f;
        public static float MinSnapDistance     => 0f;

        // Props
        public SnapMode CurrentSnapMode {
            get => m_SnapMode;
            set => m_SnapMode = value;
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ObjectToolSystem   = World.GetOrCreateSystemManaged<ObjectToolSystem>();

            // Query
            m_Query = SystemAPI.QueryBuilder()
                               .WithAllRW<ObjectDefinition>()
                               .WithAll<CreationDefinition, Updated>()
                               .WithNone<Deleted, Overridden>()
                               .Build();

            // Data
            m_SnapSetback   = DefaultSnapDistance;
            m_SnapMode      = SnapMode.RoadSide | SnapMode.ParcelFrontAlign;
            IsSnapped       = new NativeReference<bool>(Allocator.Persistent);
            IsSnapped.Value = false;

            RequireForUpdate(m_Query);
        }

        protected override void OnDestroy() {
            IsSnapped.Dispose();
            base.OnDestroy();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // todo convert to harmony patch

            // Handle vanilla line tool when not in individual plop mode
            if (m_ObjectToolSystem.actualMode is ObjectToolSystem.Mode.Create ||
                m_ObjectToolSystem.prefab is not ParcelPlaceholderPrefab parcelPrefab) {
                return;
            }

            // Override distance scale
            // ObjectToolSystem calculates distance between objects by taking distanceScale and multiplying it
            // by distance, which is based on the in-game slider, ranging from 1.5f to 6f.
            // By dividing the lot width by 1.5f, we ensure that the minimum distance on the slider creates an edge-to-edge placement.
            var width = (parcelPrefab.m_LotWidth * 8f) / 1.5f;
            m_ObjectToolSystem.SetMemberValue("distanceScale", width);
            // Patch an edge case where `distance` is set to 1.4f or lower, causing incorrect placement.
            var currentScaleMult = (float)m_ObjectToolSystem.GetMemberValue("distance");
            if (currentScaleMult < 1.6f) {
                m_ObjectToolSystem.SetMemberValue("distance", 1.5f);
            }
        }
    }
}