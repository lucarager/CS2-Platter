// <copyright file="P_TooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using Components;
    using Game.Common;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI.Localization;
    using Game.UI.Tooltip;
    using Settings;
    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Tooltip System.
    /// </summary>
    public partial class P_TooltipSystem : TooltipSystemBase {
        private InputHintTooltip      m_Tooltip_Depth;
        private InputHintTooltip      m_Tooltip_Width;
        private InputHintTooltip      m_Tooltip_Setback;
        private StringTooltip         m_Tooltip_BuildingFrontCount;
        private StringTooltip         m_Tooltip_BuildingCornerCount;
        private ObjectToolSystem      m_ObjectToolSystem;
        private P_BuildingCacheSystem m_BuildingCacheSystem;
        private P_UISystem            m_UISystem;
        private ToolSystem            m_ToolSystem;
        private EntityQuery           m_ParcelQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ObjectToolSystem    = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_BuildingCacheSystem = World.GetOrCreateSystemManaged<P_BuildingCacheSystem>();
            m_UISystem            = World.GetOrCreateSystemManaged<P_UISystem>();

            m_ParcelQuery = SystemAPI.QueryBuilder()
                                     .WithAll<Parcel>()
                                     .WithNone<Deleted>()
                                     .Build();

            m_Tooltip_Depth   = new InputHintTooltip(InputManager.instance.FindAction("Platter.Platter.PlatterMod", "BlockDepthAction"));
            m_Tooltip_Width   = new InputHintTooltip(InputManager.instance.FindAction("Platter.Platter.PlatterMod", "BlockWidthAction"));
            m_Tooltip_Setback = new InputHintTooltip(InputManager.instance.FindAction("Platter.Platter.PlatterMod", "SetbackAction"));
            m_Tooltip_BuildingFrontCount = new StringTooltip() {
                path = "Platter.BuildingCount",
                icon = "coui://platter/logo.svg",
            };
            m_Tooltip_BuildingCornerCount = new StringTooltip() {
                path = "Platter.BuildingCornerCount",
                icon = "coui://platter/logo.svg",
            };
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (!CurrentlyUsingParcelsInObjectTool(out var parcelPrefab)) {
                return;
            }

            var controlScheme  = InputManager.instance.activeControlScheme;

            if (controlScheme is not InputManager.ControlScheme.KeyboardAndMouse) {
                return;
            }

            m_Tooltip_Depth.Refresh(InputManager.DeviceType.Mouse);
            m_Tooltip_Width.Refresh(InputManager.DeviceType.Mouse);
            m_Tooltip_Setback.Refresh(InputManager.DeviceType.Mouse);

            AddMouseTooltip(m_Tooltip_Depth);
            AddMouseTooltip(m_Tooltip_Width);
            AddMouseTooltip(m_Tooltip_Setback);

            var prezone = m_UISystem.PreZoneType;

            if (prezone.m_Index == P_ZoneCacheSystem.UnzonedZoneType.m_Index) {
                return;
            }

            // Retrieve current prezone and parcel sizes
            var parcelEntities = m_ParcelQuery.ToEntityArray(Allocator.Temp);
            if (parcelEntities.Length == 0) {
                parcelEntities.Dispose();
                return;
            }

            // Check if any of the parcels have a corner designation
            var hasLeft   = false;
            var hasRight  = false;

            foreach (var entity in parcelEntities) {
                var parcel = EntityManager.GetComponentData<Parcel>(entity);

                if ((parcel.m_State & ParcelState.RoadLeft) != 0) {
                    hasLeft   = true;
                }
                if ((parcel.m_State & ParcelState.RoadRight) != 0) {
                    hasRight  = true;
                }
            }

            var count = m_BuildingCacheSystem.GetBuildingAccessCount(
                prezone.m_Index,
                parcelPrefab.m_LotWidth,
                parcelPrefab.m_LotDepth);

            // Tooltip for total front access buildings
            var locArgs = CreateBuildingCountLocArgs(count.Total, parcelPrefab.m_LotWidth, parcelPrefab.m_LotDepth);
            if (count.Total == 0) {
                m_Tooltip_BuildingFrontCount.value = new LocalizedString("PlatterMod.UI.Tooltip.BuildingCountWarning", null, locArgs);
                m_Tooltip_BuildingFrontCount.color = TooltipColor.Info;
            } else {
                m_Tooltip_BuildingFrontCount.value = new LocalizedString("PlatterMod.UI.Tooltip.BuildingCount", null, locArgs);
                m_Tooltip_BuildingFrontCount.color = TooltipColor.Success;
            }
            AddMouseTooltip(m_Tooltip_BuildingFrontCount);

            // Tooltip for total corner access buildings
            var locArgs2 = CreateBuildingCountLocArgs(count.Corner, parcelPrefab.m_LotWidth, parcelPrefab.m_LotDepth);
            if (count.Corner == 0) {
                m_Tooltip_BuildingCornerCount.value = new LocalizedString("PlatterMod.UI.Tooltip.BuildingCornerCountWarning", null, locArgs2);
                m_Tooltip_BuildingCornerCount.color = TooltipColor.Info;
            } else {
                m_Tooltip_BuildingCornerCount.value = new LocalizedString("PlatterMod.UI.Tooltip.BuildingCornerCount", null, locArgs2);
                m_Tooltip_BuildingCornerCount.color = TooltipColor.Success;
            }
            AddMouseTooltip(m_Tooltip_BuildingCornerCount);
        }

        /// <summary>
        /// Creates localization arguments for building count tooltips.
        /// </summary>
        private static Dictionary<string, ILocElement> CreateBuildingCountLocArgs(int count, int lotWidth, int lotDepth) {
            return new Dictionary<string, ILocElement> {
                { "COUNT", LocalizedString.Value(count.ToString()) },
                { "X",     LocalizedString.Value(lotWidth.ToString()) },
                { "Y",     LocalizedString.Value(lotDepth.ToString()) },
            };
        }

        /// <summary>
        /// </summary>
        private bool CurrentlyUsingParcelsInObjectTool(out ParcelPlaceholderPrefab prefab) {
            if (m_ToolSystem.activePrefab is ParcelPlaceholderPrefab activePrefab) {
                prefab = activePrefab;
                return true;
            }

            prefab = null;
            return false;
        }
    }
}