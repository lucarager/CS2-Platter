// <copyright file="PlatterUISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.UI.Binding;
    using Game.Common;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using Game.Zones;
    using Platter.Extensions;
    using Platter.Settings;
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

        public ZoneType PreZoneType { get; set; } = ZoneType.None;
        public bool AllowSpawn { get; set; } = true;

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
        private ValueBindingHelper<bool> m_EnableToolButtonsBinding;
        private ValueBindingHelper<int> m_ZoneBinding;
        private ValueBindingHelper<PrefabUIData> m_PrefabDataBinding;
        private ValueBindingHelper<ZoneUIData[]> m_ZoneDataBinding;
        private ValueBindingHelper<int> m_BlockWidthBinding;
        private ValueBindingHelper<int> m_BlockDepthBinding;
        private ValueBindingHelper<bool> m_RenderParcelsBinding;

        // Shortcuts
        private ProxyAction m_IncreaseBlockWidthAction;
        private ProxyAction m_IncreaseBlockDepthAction;

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

            // todo
            // use
            // m_ToolSystem.EventPrefabChanged += OnPrefabChanged;

            // Systems
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ZoneSystem = World.GetOrCreateSystemManaged<ZoneSystem>();
            m_PlatterOverlaySystem = World.GetOrCreateSystemManaged<PlatterOverlaySystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();

            // Bindings
            m_EnableToolButtonsBinding = CreateBinding<bool>("ENABLE_TOOL_BUTTONS", false);
            m_ZoneBinding = CreateBinding<int>("ZONE", 0, SetPreZone);
            m_BlockWidthBinding = CreateBinding<int>("BLOCK_WIDTH", 2);
            m_BlockDepthBinding = CreateBinding<int>("BLOCK_DEPTH", 2);
            PrefabUIData defaultParcel = new("Parcel 2x2", "coui://platter/Parcel_2x2.svg");
            m_PrefabDataBinding = CreateBinding<PrefabUIData>("PREFAB_DATA", defaultParcel);
            m_ZoneDataBinding = CreateBinding<ZoneUIData[]>("ZONE_DATA", m_ZoneData.ToArray());
            m_RenderParcelsBinding = CreateBinding<bool>("RENDER_PARCELS", true, SetRenderParcels);

            // Triggers
            CreateTrigger<string>("ADJUST_BLOCK_SIZE", HandleBlockSizeAdjustment);

            // Shortcuts
            m_IncreaseBlockWidthAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.IncreaseParcelWidthActionName);
            m_IncreaseBlockDepthAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.IncreaseParcelDepthActionName);
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
            var currentlyUsingParcelsInObjectTool = CurrentlyUsingParcelsInObjectTool();

            // Shortcuts
            if (m_IncreaseBlockWidthAction.WasPerformedThisFrame()) {
                m_Log.Debug(m_IncreaseBlockWidthAction.ReadValue<float>().ToString());
            }

            if (currentlyUsingParcelsInObjectTool) {
                // Make sure we refresh the lot sizes if the Object Tool is active
                m_SelectedParcelSize.x = ((ParcelPrefab)m_ObjectToolSystem.prefab).m_LotWidth;
                m_SelectedParcelSize.y = ((ParcelPrefab)m_ObjectToolSystem.prefab).m_LotDepth;
            }

            // Rendering should be on when using the tool
            SetRenderParcels(currentlyUsingParcelsInObjectTool);

            // Update Bindings
            m_RenderParcelsBinding.Value = m_PlatterOverlaySystem.RenderParcels;
            m_BlockWidthBinding.Value = m_SelectedParcelSize.x;
            m_BlockDepthBinding.Value = m_SelectedParcelSize.y;
            m_EnableToolButtonsBinding.Value = currentlyUsingParcelsInObjectTool;

            // Send down zone data when ready
            if (m_ZoneDataBinding.Value.Length == 0) {
                m_ZoneDataBinding.Value = m_ZoneData.ToArray();
            }

            // Load zone data if unavailable
            if (!m_ZoneQuery.IsEmptyIgnoreFilter && loadZones) {
                LoadZoneData();
            }

            // Update selected Prefab binding
            var prefabBase = m_ObjectToolSystem.prefab;
            if (prefabBase != null) {
                m_PrefabDataBinding.Value = new PrefabUIData(prefabBase.name, ImageSystem.GetThumbnail(prefabBase));
            }
        }

        /// <summary>
        /// </summary>
        private bool CurrentlyUsingParcelsInObjectTool() {
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
        private void SetRenderParcels(bool enabled) {            
            m_PlatterOverlaySystem.RenderParcels = enabled;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetPreZone(int zoneIndex) {
            m_Log.Debug($"SetPreZone(modeIndex = {zoneIndex})");
            PreZoneType = m_ZoneTypeData[zoneIndex];
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
