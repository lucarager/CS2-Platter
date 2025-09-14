// <copyright file="PlatterUISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.UI.Binding;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using Game.Zones;
    using Platter.Extensions;
    using Platter.Utils;
    using System;
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class PlatterUISystem : ExtendedUISystemBase {
        /// <summary>
        /// Todo.
        /// </summary>
        private readonly PlatterToolSystem m_PlatterToolSystem = PlatterToolSystem.Instance;

        // Systems
        private PrefabSystem m_PrefabSystem;
        private ToolSystem m_ToolSystem;
        private ZoneSystem m_ZoneSystem;
        private PlatterOverlaySystem m_PlatterOverlaySystem;
        private ObjectToolSystem m_ObjectToolSystem;

        // Queries
        private EntityQuery m_ZoneQuery;

        // Logger
        private PrefixedLogger m_Log;

        // Data
        private int2 m_SelectedParcelSize = new(2, 2);

        // Bindings
        private ValueBindingHelper<bool> m_ToolEnabledBinding;
        private ValueBindingHelper<bool> m_EnableToolButtonsBinding;
        private ValueBindingHelper<int> m_ToolModeBinding;
        private ValueBindingHelper<int> m_ZoneBinding;
        private ValueBindingHelper<int> m_PointsCountBinding;
        private ValueBindingHelper<PrefabUIData> m_PrefabDataBinding;
        private ValueBindingHelper<ZoneUIData[]> m_ZoneDataBinding;
        private ValueBindingHelper<int> m_BlockWidthBinding;
        private ValueBindingHelper<int> m_BlockDepthBinding;
        private ValueBindingHelper<float> m_RoadEditorSpacingBinding;
        private ValueBindingHelper<float> m_RoadEditorOffsetBinding;
        private ValueBindingHelper<bool[]> m_RoadEditorSideBinding;
        private ValueBindingHelper<bool> m_RenderParcelsBinding;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(PlatterUISystem));
            m_Log.Debug($"OnCreate()");
            m_ZoneData = new();
            m_ZoneTypeData = new();

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
            m_PlatterOverlaySystem = World.GetOrCreateSystemManaged<PlatterOverlaySystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();

            // Bindings
            m_EnableToolButtonsBinding = CreateBinding<bool>("ENABLE_TOOL_BUTTONS", false);
            m_ToolEnabledBinding = CreateBinding<bool>("TOOL_ENABLED", false, SetTool);
            m_ToolModeBinding = CreateBinding<int>("TOOL_MODE", 0, SetToolMode);
            m_ZoneBinding = CreateBinding<int>("ZONE", 0, SetPreZone);
            m_PointsCountBinding = CreateBinding<int>("POINTS_COUNT", 0);
            m_BlockWidthBinding = CreateBinding<int>("BLOCK_WIDTH", 2);
            m_BlockDepthBinding = CreateBinding<int>("BLOCK_DEPTH", 2);
            PrefabUIData defaultParcel = new("Parcel 2x2", "coui://platter/Parcel_2x2.svg");
            m_PrefabDataBinding = CreateBinding<PrefabUIData>("PREFAB_DATA", defaultParcel);
            m_ZoneDataBinding = CreateBinding<ZoneUIData[]>("ZONE_DATA", m_ZoneData.ToArray());
            m_RenderParcelsBinding = CreateBinding<bool>("RENDER_PARCELS", true, SetRenderParcels);

            // Road Editor
            m_RoadEditorSideBinding = CreateBinding<bool[]>("RE_SIDES", new bool[4] { true, true, false, false }, SetSides);
            m_RoadEditorSpacingBinding = CreateBinding<float>("RE_SPACING", 1f, SetSpacing);
            m_RoadEditorOffsetBinding = CreateBinding<float>("RE_OFFSET", 2f, SetOffset);

            CreateTrigger<string>("ADJUST_BLOCK_SIZE", HandleBlockSizeAdjustment);
            CreateTrigger("REQUEST_APPLY", RequestApply);
        }

        private List<ZoneUIData> m_ZoneData;
        private Dictionary<int, ZoneType> m_ZoneTypeData;
        private bool loadZones = true;

        private void LoadZoneData() {
            loadZones = false;
            var entities = m_ZoneQuery.ToEntityArray(Allocator.TempJob);

            foreach (var zonePrefabEntity in entities) {
                var prefabData = EntityManager.GetComponentData<PrefabData>(zonePrefabEntity);
                var zoneData = EntityManager.GetComponentData<ZoneData>(zonePrefabEntity);
                var zonePrefab = m_PrefabSystem.GetPrefab<ZonePrefab>(prefabData);

                m_Log.Debug($"Adding zone {zonePrefab.name} {ImageSystem.GetThumbnail(zonePrefab)}");
                m_ZoneTypeData.Add(zoneData.m_ZoneType.m_Index, zoneData.m_ZoneType);
                m_ZoneData.Add(new ZoneUIData(zonePrefab.name, ImageSystem.GetThumbnail(zonePrefab), zoneData.m_ZoneType.m_Index));
            }

            entities.Dispose();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_ToolEnabledBinding.Value = m_PlatterToolSystem.Enabled;
            m_RenderParcelsBinding.Value = m_PlatterOverlaySystem.RenderParcels;
            m_BlockWidthBinding.Value = m_SelectedParcelSize.x;
            m_BlockDepthBinding.Value = m_SelectedParcelSize.y;
            m_EnableToolButtonsBinding.Value = EnableToolButtons();

            if (m_ZoneDataBinding.Value.Length == 0) {
                m_ZoneDataBinding.Value = m_ZoneData.ToArray();
            }

            if (!m_ZoneQuery.IsEmptyIgnoreFilter && loadZones) {
                LoadZoneData();
            }

            if (!m_PlatterToolSystem.Enabled) {
                return;
            }

            m_ToolModeBinding.Value = (int)m_PlatterToolSystem.CurrentMode;

            m_RoadEditorSpacingBinding.Value = m_PlatterToolSystem.RoadEditorSpacing;
            m_RoadEditorOffsetBinding.Value = m_PlatterToolSystem.RoadEditorOffset;
            m_RoadEditorSideBinding.Value = new bool[] {
                m_PlatterToolSystem.RoadEditorSides.x,
                m_PlatterToolSystem.RoadEditorSides.y,
                m_PlatterToolSystem.RoadEditorSides.z,
                m_PlatterToolSystem.RoadEditorSides.w
            };

            m_PointsCountBinding.Value = m_PlatterToolSystem.m_Points.Count;



            var prefabBase = m_PlatterToolSystem.SelectedPrefab;
            if (prefabBase != null) {
                m_PrefabDataBinding.Value = new PrefabUIData(prefabBase.name, ImageSystem.GetThumbnail(prefabBase));
            }
        }

        /// <summary>
        /// </summary>
        private bool EnableToolButtons() {
            if (m_ToolSystem.activeTool is ObjectToolSystem objectToolSystem && m_ObjectToolSystem.prefab is ParcelPrefab) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Open the panel.
        /// </summary>
        private void HandleBlockSizeAdjustment(string action) {
            m_Log.Debug($"HandleBlockSizeAdjustment(action: {action})");

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
        /// Called from the UI.
        /// </summary>
        private void ToggleTool() {
            m_Log.Debug($"ToggleTool()");

            m_PlatterToolSystem.RequestToggle();
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetTool(bool enabled) {
            if (enabled) {
                m_PlatterToolSystem.RequestEnable();
            } else {
                m_PlatterToolSystem.RequestDisable();
            }
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetRenderParcels(bool enabled) {
            m_Log.Debug($"SetRenderParcels(enabled = {enabled})");
            m_PlatterOverlaySystem.RenderParcels = enabled;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetToolMode(int modeIndex) {
            var mode = (PlatterToolSystem.PlatterToolMode)modeIndex;
            m_Log.Debug($"SetToolMode(modeIndex = {modeIndex}, mode = {mode})");

            m_PlatterToolSystem.CurrentMode = mode;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetPreZone(int zoneIndex) {
            m_Log.Debug($"SetPreZone(modeIndex = {zoneIndex})");

            m_PlatterToolSystem.PreZoneType = m_ZoneTypeData[zoneIndex];
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetSpacing(float spacing) {
            m_Log.Debug($"SetSpacing(spacing = {spacing})");

            m_PlatterToolSystem.RoadEditorSpacing = spacing;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetSides(bool[] sides) {
            m_Log.Debug($"SetSides(sides = {sides})");

            if (sides.Length != 4) {
                return;
            }

            m_PlatterToolSystem.RoadEditorSides = new bool4(sides[0], sides[1], sides[2], sides[3]);
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetOffset(float offset) {
            m_Log.Debug($"SetSpacing(offset = {offset})");

            m_PlatterToolSystem.RoadEditorOffset = offset;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void RequestApply() {
            m_PlatterToolSystem.ApplyWasRequested = true;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void DecreaseBlockWidth() {
            if (m_SelectedParcelSize.x > PlatterPrefabSystem.BlockSizes.x) {
                m_SelectedParcelSize.x -= 1;
            }

            m_Log.Debug("DecreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void IncreaseBlockWidth() {
            if (m_SelectedParcelSize.x < PlatterPrefabSystem.BlockSizes.z) {
                m_SelectedParcelSize.x += 1;
            }

            m_Log.Debug("IncreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void DecreaseBlockDepth() {
            if (m_SelectedParcelSize.y > PlatterPrefabSystem.BlockSizes.y) {
                m_SelectedParcelSize.y -= 1;
            }

            m_Log.Debug("DecreaseBlockDepth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void IncreaseBlockDepth() {
            if (m_SelectedParcelSize.y < PlatterPrefabSystem.BlockSizes.w) {
                m_SelectedParcelSize.y += 1;
            }

            m_Log.Debug("IncreaseBlockDepth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        private void UpdateSelectedPrefab() {
            m_Log.Debug($"UpdateSelectedPrefab() -- {m_SelectedParcelSize.x}x{m_SelectedParcelSize.y}");

            // Todo abstract this
            var id = new PrefabID("ParcelPrefab", $"Parcel {m_SelectedParcelSize.x}x{m_SelectedParcelSize.y}");

            m_Log.Debug($"UpdateSelectedPrefab() -- Attempting to get Prefab with id {id}");

            if (!m_PrefabSystem.TryGetPrefab(id, out var prefabBase)) {
                m_Log.Debug($"UpdateSelectedPrefab() -- Couldn't find prefabBase!");
                return;
            }

            m_Log.Debug($"UpdateSelectedPrefab() -- Found ${prefabBase}!");

            var result = m_ObjectToolSystem.TrySetPrefab(prefabBase);

            m_Log.Debug($"UpdateSelectedPrefab() -- Result {result}");
        }
    }
}
