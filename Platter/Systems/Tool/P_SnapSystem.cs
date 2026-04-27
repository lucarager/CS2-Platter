// <copyright file="P_SnapSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Extensions;
    using Unity.Collections;

    #endregion

    /// <summary>
    ///     Ovverides object placement to snap parcels to road sides
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

        public  NativeReference<bool> IsSnapped;
        private ObjectToolSystem      m_ObjectToolSystem;
        private ToolSystem            m_ToolSystem;
        private SnapMode              m_SnapMode;
        private float m_SnapSetback;
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

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            // Data
            m_SnapSetback = DefaultSnapDistance;
            m_SnapMode      = SnapMode.RoadSide | SnapMode.ParcelFrontAlign;
            IsSnapped       = new NativeReference<bool>(Allocator.Persistent);
            IsSnapped.Value = false;
        }

        protected override void OnDestroy() {
            IsSnapped.Dispose();
            base.OnDestroy();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            var isObjectToolActive = m_ToolSystem.activeTool is ObjectToolSystem;
            var isUsingPlatter     = m_ObjectToolSystem.prefab is ParcelPlaceholderPrefab;
            var isPlacingSingle    = m_ObjectToolSystem.actualMode is not ObjectToolSystem.Mode.Create;

            // Handle vanilla line tool when not in individual plop mode
            if (!isPlacingSingle && isUsingPlatter) {
                var parcelPrefab = (ParcelPlaceholderPrefab)m_ObjectToolSystem.prefab;
                var width = (parcelPrefab.m_LotWidth * 8f);
                // We manually patch the distance scale for the line tool to allow parcels to be edge to edge
                m_ObjectToolSystem.SetMemberValue("distanceScale", width);
            }

            // Reset isSnapped when not using platter
            if (!isObjectToolActive || !isUsingPlatter) {
                IsSnapped.Value = false;
            }

        }

        private static class SnapLevel {
            public const float None             = 0f;
            public const float ParcelEdge       = 1f;
            public const float RoadSide         = 2f;
            public const float ParcelFrontAlign = 3f;
            public const float ZoneSide         = 3f;
        }
    }
}
