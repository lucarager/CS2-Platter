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
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_ToolEnabledBinding.Update(m_ParcelToolSystem.Enabled);

            if (m_ParcelToolSystem.Enabled) {
                var prefabBase = m_ParcelToolSystem.Prefab;
                if (prefabBase != null) {
                    m_PrefabDataBinding.Update(new PrefabUIData(prefabBase.name, ImageSystem.GetThumbnail(prefabBase)));
                }
            }
        }

        /// <summary>
        /// Open the panel.
        /// </summary>
        private void HandleButtonPress(string action) {
            m_Log.Debug($"HandleButtonPress(action: {action})");

            switch (action) {
                case "BLOCK_WIDTH_INCREASE":
                    m_ParcelToolSystem.IncreaseBlockWidth();
                    break;
                case "BLOCK_WIDTH_DECREASE":
                    m_ParcelToolSystem.DecreaseBlockWidth();
                    break;
                case "BLOCK_DEPTH_INCREASE":
                    m_ParcelToolSystem.IncreaseBlockDepth();
                    break;
                case "BLOCK_DEPTH_DECREASE":
                    m_ParcelToolSystem.DecreaseBlockDepth();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void ToggleTool() {
            m_ParcelToolSystem.RequestToggle();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="prefabBase">Todo.</param>
        // internal void TryActivatePrefabTool(PrefabBase prefabBase) {
        //    m_Log.Debug($"TryActivatePrefabTool(prefabBase {prefabBase}) -- {m_SelectedBlockSize.x}x{m_SelectedBlockSize.y}");

        // if (m_ParcelToolSystem.TrySetPrefab(prefabBase)) {
        //        m_ParcelToolSystem.RequestEnable();
        //    } else {
        //        m_ToolSystem.ActivatePrefabTool(prefabBase);
        //    }
        // }
    }
}
