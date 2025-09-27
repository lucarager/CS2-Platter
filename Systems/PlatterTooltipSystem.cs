// <copyright file="PlatterTooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI.Tooltip;
    using Platter.Settings;
    using Unity.Entities;

    /// <summary>
    /// Tooltip System.
    /// </summary>
    public partial class PlatterTooltipSystem : TooltipSystemBase {
        private ToolSystem m_ToolSystem;
        private ObjectToolSystem m_ObjectToolSystem;
        private InputHintTooltip m_Tooltip_IncreaseWidth;
        private InputHintTooltip m_Tooltip_DecreaseWidth;
        private InputHintTooltip m_Tooltip_IncreaseDepth;
        private InputHintTooltip m_Tooltip_DecreaseDepth;
        private ProxyAction m_IncreaseWidthAction;
        private ProxyAction m_DecreaseWidthAction;
        private ProxyAction m_IncreaseDepthAction;
        private ProxyAction m_DecreaseDepthAction;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_IncreaseWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelWidthActionName);
            m_DecreaseWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelWidthActionName);
            m_IncreaseDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelDepthActionName);
            m_DecreaseDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelDepthActionName);

            m_Tooltip_IncreaseWidth = new InputHintTooltip(m_IncreaseDepthAction, InputManager.DeviceType.Mouse);
            m_Tooltip_DecreaseWidth = new InputHintTooltip(m_DecreaseDepthAction, InputManager.DeviceType.Mouse);
            m_Tooltip_IncreaseDepth = new InputHintTooltip(m_IncreaseWidthAction, InputManager.DeviceType.Mouse);
            m_Tooltip_DecreaseDepth = new InputHintTooltip(m_DecreaseWidthAction, InputManager.DeviceType.Mouse);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (CurrentlyUsingParcelsInObjectTool()) {
                AddMouseTooltip(m_Tooltip_IncreaseWidth);
                AddMouseTooltip(m_Tooltip_DecreaseWidth);
                AddMouseTooltip(m_Tooltip_IncreaseDepth);
                AddMouseTooltip(m_Tooltip_DecreaseDepth);
            }
        }

        /// <summary>
        /// </summary>
        private bool CurrentlyUsingParcelsInObjectTool() {
            return m_ToolSystem.activeTool is ObjectToolSystem && m_ObjectToolSystem.prefab is ParcelPrefab;
        }
    }
}