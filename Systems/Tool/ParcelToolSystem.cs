// <copyright file="ParcelToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Annotations;
    using Game.Areas;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Utils;
    using System;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class ParcelToolSystem : ToolBaseSystem {
        /// <inheritdoc/>
        public override string toolID => "Parcel Tool";

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

        // Data
        private int2 m_SelectedBlockSize = new(2, 2);
        private ObjectPrefab m_SelectedPrefab;
        private TransformPrefab m_TransformPrefab;

        [CanBeNull]
        public ObjectPrefab Prefab {
            get => this.m_SelectedPrefab;
            set {
                if (value != this.m_SelectedPrefab) {
                    this.m_SelectedPrefab = value;
                    this.m_ForceUpdate = true;
                    if (value != null) {
                        this.m_TransformPrefab = null;
                    }

                    Action<PrefabBase> eventPrefabChanged = this.m_ToolSystem.EventPrefabChanged;
                    if (eventPrefabChanged == null) {
                        return;
                    }

                    eventPrefabChanged(value);
                }
            }
        }

        public TransformPrefab Transform {
            get => this.m_TransformPrefab;
            set {
                if (value != this.m_TransformPrefab) {
                    this.m_TransformPrefab = value;
                    this.m_ForceUpdate = true;
                    if (value != null) {
                        this.m_SelectedPrefab = null;
                        Action<PrefabBase> eventPrefabChanged = this.m_ToolSystem.EventPrefabChanged;
                        if (eventPrefabChanged == null) {
                            return;
                        }

                        eventPrefabChanged(value);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override Game.Prefabs.PrefabBase GetPrefab() {
            return !(this.Prefab != null) ? this.Transform : this.Prefab;
        }

        /// <inheritdoc/>
        public override bool TrySetPrefab(Game.Prefabs.PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab(prefab {prefab})");

            ObjectPrefab objectPrefab = prefab as ObjectPrefab;
            if (objectPrefab != null) {
                Prefab = objectPrefab;
                return true;
            }

            TransformPrefab transformPrefab = prefab as TransformPrefab;
            if (transformPrefab != null) {
                Transform = transformPrefab;
                return true;
            }

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

            base.OnStartRunning();
            base.requireZones = true;
            base.requireAreas = AreaTypeMask.Lots;
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

        /// <summary>
        /// Todo.
        /// </summary>
        public void DecreaseBlockWidth() {
            if (m_SelectedBlockSize.x > PrefabLoadSystem.BlockSizes.x) {
                m_SelectedBlockSize.x -= 1;
            }

            m_Log.Debug("DecreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void IncreaseBlockWidth() {
            if (m_SelectedBlockSize.x < PrefabLoadSystem.BlockSizes.z) {
                m_SelectedBlockSize.x += 1;
            }

            m_Log.Debug("IncreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void DecreaseBlockDepth() {
            if (m_SelectedBlockSize.x > PrefabLoadSystem.BlockSizes.y) {
                m_SelectedBlockSize.x -= 1;
            }

            m_Log.Debug("DecreaseBlockDepth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void IncreaseBlockDepth() {
            if (m_SelectedBlockSize.x < PrefabLoadSystem.BlockSizes.w) {
                m_SelectedBlockSize.x += 1;
            }

            m_Log.Debug("IncreaseBlockDepth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        private void UpdateSelectedPrefab() {
            m_Log.Debug($"UpdateSelectedPrefab() -- {m_SelectedBlockSize.x}x{m_SelectedBlockSize.y}");

            // Todo abstract this
            var id = new PrefabID("ParcelPrefab", $"Parcel {m_SelectedBlockSize.x}x{m_SelectedBlockSize.y}");

            m_Log.Debug($"UpdateSelectedPrefab() -- Attempting to get Prefab with id {id}");

            if (!m_PrefabSystem.TryGetPrefab(id, out var prefabBase)) {
                m_Log.Debug($"UpdateSelectedPrefab() -- Couldn't find prefabBase!");
                return;
            }

            TrySetPrefab(prefabBase);

            // m_Prefab = prefabBase;
            // m_PrefabDataBinding.Update(new PrefabUIData(prefabBase.name, ImageSystem.GetThumbnail(prefabBase)));
            // TryActivatePrefabTool(m_Prefab);
        }
    }
}
