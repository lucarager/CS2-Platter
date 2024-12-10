// <copyright file="ParcelToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game.Tools;
    using Platter.Utils;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ParcelToolSystem : ObjectToolBaseSystem {
        /// <summary>
        /// Instance.
        /// </summary>
        public static ParcelToolSystem m_Instance;

        // Logger
        private PrefixedLogger m_Log;

        // Actions

        // Systems & References
        private Game.Common.RaycastSystem m_RaycastSystem;

        // Jobs
        private JobHandle m_InputDeps;

        /// <inheritdoc/>
        public override string toolID => "ParcelToolSystem";

        /// <inheritdoc/>
        public override Game.Prefabs.PrefabBase GetPrefab() {
            return null;
        }

        /// <inheritdoc/>
        public override bool TrySetPrefab(Game.Prefabs.PrefabBase prefab) {
            return false;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void RequestEnable() {
            m_Log.Debug($"RequestEnable()");

            if (m_ToolSystem.activeTool != this) {
                m_ToolSystem.selected = Entity.Null;
                m_ToolSystem.activeTool = this;
            }
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void RequestDisable() {
            m_Log.Debug($"RequestDisable()");

            m_ToolSystem.activeTool = m_DefaultToolSystem;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void RequestToggle() {
            m_Log.Debug($"RequestToggle()");

            if (m_ToolSystem.activeTool == this) {
                RequestDisable();
            } else {
                RequestEnable();
            }
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            m_Instance = this;
            Enabled = false;

            // Logging
            m_Log = new PrefixedLogger(nameof(ParcelToolSystem));
            m_Log.Debug($"OnCreate()");

            // Get Systems
            m_RaycastSystem = World.GetOrCreateSystemManaged<Game.Common.RaycastSystem>();

            base.OnCreate();
        }

        /// <inheritdoc/>
        protected override void OnStartRunning() {
            m_Log.Debug($"OnStartRunning()");
        }

        /// <inheritdoc/>
        protected override void OnStopRunning() {
            m_Log.Debug($"OnStopRunning()");
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_Log.Debug($"OnDestroy()");

            base.OnDestroy();
        }

        /// <inheritdoc/>
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            m_InputDeps = base.OnUpdate(inputDeps);
            return inputDeps;
        }
    }
}
