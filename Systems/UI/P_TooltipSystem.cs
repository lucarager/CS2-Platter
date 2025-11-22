// <copyright file="P_TooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI.Localization;
    using Game.UI.Tooltip;
    using Settings;

    #endregion

    /// <summary>
    /// Tooltip System.
    /// </summary>
    public partial class P_TooltipSystem : TooltipSystemBase {
        private InputHintTooltip      m_Tooltip_DecreaseDepth;
        private InputHintTooltip      m_Tooltip_DecreaseWidth;
        private InputHintTooltip      m_Tooltip_IncreaseDepth;
        private InputHintTooltip      m_Tooltip_IncreaseWidth;
        private ObjectToolSystem      m_ObjectToolSystem;
        private P_BuildingCacheSystem m_BuildingCacheSystem;
        private P_UISystem            m_UISystem;
        private ProxyAction           m_DecreaseDepthAction;
        private ProxyAction           m_DecreaseWidthAction;
        private ProxyAction           m_IncreaseDepthAction;
        private ProxyAction           m_IncreaseWidthAction;
        private StringTooltip         m_Tooltip_BuildingCount;
        private ToolSystem            m_ToolSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ObjectToolSystem    = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_BuildingCacheSystem = World.GetOrCreateSystemManaged<P_BuildingCacheSystem>();
            m_UISystem            = World.GetOrCreateSystemManaged<P_UISystem>();
            m_IncreaseWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelWidthActionName);
            m_DecreaseWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelWidthActionName);
            m_IncreaseDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelDepthActionName);
            m_DecreaseDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelDepthActionName);

            m_Tooltip_IncreaseWidth = new InputHintTooltip(
                m_IncreaseDepthAction,
                InputManager.DeviceType.Keyboard);
            m_Tooltip_DecreaseWidth = new InputHintTooltip(
                m_DecreaseDepthAction,
                InputManager.DeviceType.Keyboard);
            m_Tooltip_IncreaseDepth = new InputHintTooltip(
                m_IncreaseWidthAction,
                InputManager.DeviceType.Keyboard);
            m_Tooltip_DecreaseDepth = new InputHintTooltip(
                m_DecreaseWidthAction,
                InputManager.DeviceType.Keyboard);
            m_Tooltip_BuildingCount = new StringTooltip();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (!CurrentlyUsingParcelsInObjectTool()) {
                return;
            }

            AddMouseTooltip(m_Tooltip_IncreaseWidth);
            AddMouseTooltip(m_Tooltip_DecreaseWidth);
            AddMouseTooltip(m_Tooltip_IncreaseDepth);
            AddMouseTooltip(m_Tooltip_DecreaseDepth);

            var prezone = m_UISystem.PreZoneType;

            if (prezone.m_Index == 0) {
                return;
            }

            // Retrieve current prezone and parcel sizes
            var parcel = (ParcelPlaceholderPrefab)m_ObjectToolSystem.prefab;

            var count = m_BuildingCacheSystem.GetBuildingCount(
                prezone.m_Index,
                parcel.m_LotWidth,
                parcel.m_LotDepth);

            if (count == 0) {
                // Add warning tooltip
                m_Tooltip_BuildingCount.value = new LocalizedString(
                    "PlatterMod.UI.Tooltip.BuildingCountWarning",
                    null,
                    new Dictionary<string, ILocElement> {
                        {
                            "COUNT",
                            LocalizedString.Value(count.ToString())
                        }, {
                            "X",
                            LocalizedString.Value(parcel.m_LotWidth.ToString())
                        }, {
                            "Y",
                            LocalizedString.Value(parcel.m_LotDepth.ToString())
                        },
                    });
                m_Tooltip_BuildingCount.icon  = "coui://uil/Standard/ExclamationMark.svg";
                m_Tooltip_BuildingCount.color = TooltipColor.Warning;
            } else {
                m_Tooltip_BuildingCount.value = new LocalizedString(
                    "PlatterMod.UI.Tooltip.BuildingCount",
                    null,
                    new Dictionary<string, ILocElement> {
                        {
                            "COUNT",
                            LocalizedString.Value(count.ToString())
                        }, {
                            "X",
                            LocalizedString.Value(parcel.m_LotWidth.ToString())
                        }, {
                            "Y",
                            LocalizedString.Value(parcel.m_LotDepth.ToString())
                        },
                    });
                m_Tooltip_BuildingCount.icon  = "coui://uil/Standard/CircleInfo.svg";
                m_Tooltip_BuildingCount.color = TooltipColor.Success;
            }

            AddMouseTooltip(m_Tooltip_BuildingCount);
        }

        /// <summary>
        /// </summary>
        private bool CurrentlyUsingParcelsInObjectTool() { return m_ToolSystem.activePrefab is ParcelPlaceholderPrefab; }
    }
}