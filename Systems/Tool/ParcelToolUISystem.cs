// <copyright file="ParcelToolUISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.UI.Binding;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using Platter;
    using Platter.Utils;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// todo.
    /// </summary>
    internal partial class ParcelToolUISystem : UISystemBase {
        /// <summary>
        /// Todo.
        /// </summary>
        private readonly ParcelToolSystem m_ParcelToolSystem = ParcelToolSystem.m_Instance;

        // Systems
        private PrefabSystem m_PrefabSystem;
        private ToolSystem m_ToolSystem;

        // Logger
        private PrefixedLogger m_Log;

        // Bindings
        private ValueBinding<bool> m_ToolEnabledBinding;
        private ValueBinding<PrefabUIData> m_PrefabDataBinding;

        // Data
        private int2 m_SelectedBlockSize = new(2, 2);
        private PrefabBase m_Prefab;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ParcelToolUISystem));
            m_Log.Debug($"OnCreate()");

            // Systems
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Bindings
            AddBinding(m_ToolEnabledBinding = new ValueBinding<bool>(PlatterMod.Id, "BINDING:TOOL_ENABLED", false));
            AddBinding(m_PrefabDataBinding = new ValueBinding<PrefabUIData>(PlatterMod.Id, "BINDING:PREFAB_DATA", default));
            AddBinding(new TriggerBinding(PlatterMod.Id, "EVENT:TOGGLE_TOOL", ToggleTool));
            AddBinding(new TriggerBinding<string>(PlatterMod.Id, "EVENT:BUTTON_PRESS", HandleButtonPress));

            UpdateSelectedPrefab();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (m_ParcelToolSystem.Enabled) {
                // _PanelState.Update();
            }

            m_ToolEnabledBinding.Update(m_ParcelToolSystem.Enabled);
        }

        /// <summary>
        /// Open the panel.
        /// </summary>
        private void HandleButtonPress(string action) {
            m_Log.Debug($"HandleButtonPress(action: {action})");

            switch (action) {
                case "BLOCK_WIDTH_INCREASE":
                    IncreaseBlockWidth();
                    break;
                case "BLOCK_WIDTH_DECREASE":
                    DecreaseBlockWidth();
                    break;
                case "BLOCK_DEPTH_INCREASE":
                    IncreaseBlockDepth();
                    break;
                case "BLOCK_DEPTH_DECREASE":
                    DecreaseBlockDepth();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Todo.
        /// </summary>
        private void DecreaseBlockWidth() {
            if (m_SelectedBlockSize.x > PrefabLoadSystem.BlockSizes.x) {
                m_SelectedBlockSize.x -= 1;
            }

            m_Log.Debug("DecreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        private void IncreaseBlockWidth() {
            if (m_SelectedBlockSize.x < PrefabLoadSystem.BlockSizes.z) {
                m_SelectedBlockSize.x += 1;
            }

            m_Log.Debug("IncreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        private void DecreaseBlockDepth() {
            if (m_SelectedBlockSize.x > PrefabLoadSystem.BlockSizes.y) {
                m_SelectedBlockSize.x -= 1;
            }

            m_Log.Debug("DecreaseBlockDepth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        private void IncreaseBlockDepth() {
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
            var id = new PrefabID("StaticObjectPrefab", $"Parcel {m_SelectedBlockSize.x}x{m_SelectedBlockSize.y}");
            if (!m_PrefabSystem.TryGetPrefab(id, out var prefabBase)) {
                m_Log.Debug($"UpdateSelectedPrefab() -- Couldn't find prefabBase!");
                return;
            }

            m_Prefab = prefabBase;
            m_PrefabDataBinding.Update(new PrefabUIData(prefabBase.name, ImageSystem.GetThumbnail(prefabBase)));

            TryActivatePrefabTool(m_Prefab);
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void ToggleTool() {
            m_ParcelToolSystem.RequestToggle();
        }

        internal void TryActivatePrefabTool(PrefabBase prefabBase) {
            m_Log.Debug($"TryActivatePrefabTool(prefabBase {prefabBase}) -- {m_SelectedBlockSize.x}x{m_SelectedBlockSize.y}");

            // Activates a prefabBase using its index from PrefabIndexingSystem
            m_ToolSystem.ActivatePrefabTool(prefabBase);
        }
    }
}
