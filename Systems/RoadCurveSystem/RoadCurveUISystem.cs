// <copyright file="RoadCurveUISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Extensions;
    using Platter.Utils;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class RoadCurveUISystem : ExtendedUISystemBase {
        /// <summary>
        /// Todo.
        /// </summary>
        private readonly RoadCurveToolSystem m_RoadCurveToolSystem = RoadCurveToolSystem.Instance;

        // Systems
        private PrefabSystem m_PrefabSystem;
        private ToolSystem m_ToolSystem;
        private ZoneSystem m_ZoneSystem;

        // Queries
        private EntityQuery m_ZoneQuery;

        // Logger
        private PrefixedLogger m_Log;

        // Bindings
        private ValueBindingHelper<bool> m_ToolEnabledBinding;
        private ValueBindingHelper<float> m_StartEdgeBinding;
        private ValueBindingHelper<float> m_StartHandleBinding;
        private ValueBindingHelper<float> m_EndNodeBinding;
        private ValueBindingHelper<float> m_EndHandleBinding;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(PlatterUISystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_ZoneQuery = GetEntityQuery(new ComponentType[]
            {
                            ComponentType.ReadOnly<ZoneData>(),
                            ComponentType.ReadOnly<PrefabData>(),
                            ComponentType.Exclude<Deleted>()
            });

            // Systems
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ZoneSystem = World.GetOrCreateSystemManaged<ZoneSystem>();

            // Bindings
            m_ToolEnabledBinding = CreateBinding<bool>("EE_TOOL_ENABLED", false, SetTool);
            m_StartEdgeBinding = CreateBinding<float>("EE_START_NODE", 0f, SetNodeData);
            m_StartHandleBinding = CreateBinding<float>("EE_START_HANDLE", 0f, SetNodeData);
            m_EndNodeBinding = CreateBinding<float>("EE_END_NODE", 0f, SetNodeData);
            m_EndHandleBinding = CreateBinding<float>("EE_END_HANDLE", 0f, SetNodeData);

            // CreateTrigger<string>("ADJUST_BLOCK_SIZE", HandleBlockSizeAdjustment);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_ToolEnabledBinding.Value = m_RoadCurveToolSystem.Enabled;

            if (m_RoadCurveToolSystem.Enabled) {
            }
        }

        private void SetNodeData(float number) {
            m_Log.Debug($"SetNodeData(number = {number})");
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetTool(bool enabled) {
            if (enabled) {
                m_RoadCurveToolSystem.RequestEnable();
            } else {
                m_RoadCurveToolSystem.RequestDisable();
            }
        }
    }
}
