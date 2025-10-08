// <copyright file="P_UISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Colossal.Entities;
    using Colossal.Serialization.Entities;
    using Colossal.UI.Binding;
    using Game;
    using Game.City;
    using Game.Common;
    using Game.Input;
    using Game.Prefabs;
    using Game.Prefabs.Modes;
    using Game.Tools;
    using Game.UI;
    using Game.Zones;
    using Platter.Extensions;
    using Platter.Settings;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class P_UISystem : ExtendedUISystemBase {
        public ZoneType PreZoneType { get; set; } = ZoneType.None;

        public bool AllowSpawning { get; set; } = true;

        // Systems
        private PrefabSystem m_PrefabSystem;
        private ToolSystem m_ToolSystem;
        private ObjectToolSystem m_ObjectToolSystem;
        private P_OverlaySystem m_PlatterOverlaySystem;
        private P_ParcelSpawnSystem m_ParcelSpawnSystem;
        private P_SnapSystem m_SnapSystem;
        private P_ZoneCacheSystem m_ZoneCacheSystem;

        // Queries
        private EntityQuery m_Query;

        // Logger
        private PrefixedLogger m_Log;

        // Data
        private int2 m_SelectedParcelSize = new (2, 2);

        // Bindings
        private ValueBindingHelper<bool> m_EnableToolButtonsBinding;
        private ValueBindingHelper<int> m_ZoneBinding;
        private ValueBindingHelper<ZoneUIData[]> m_ZoneDataBinding;
        private ValueBindingHelper<int> m_BlockWidthBinding;
        private ValueBindingHelper<int> m_BlockDepthBinding;
        private ValueBindingHelper<bool> m_RenderParcelsBinding;
        private ValueBindingHelper<bool> m_AllowSpawningBinding;
        private ValueBindingHelper<bool> m_SnapRoadSideBinding;
        private ValueBindingHelper<float> m_SnapSpacingBinding;

        // Shortcuts
        private ProxyAction m_IncreaseBlockWidthAction;
        private ProxyAction m_IncreaseBlockDepthAction;
        private ProxyAction m_DecreaseBlockWidthAction;
        private ProxyAction m_DecreaseBlockDepthAction;
        private ProxyAction m_ToggleRender;
        private ProxyAction m_ToggleSpawn;

        /// <summary>
        /// Todo.
        /// </summary>
        public readonly struct ZoneUIData : IJsonWritable {
            public readonly string Name;
            public readonly string Thumbnail;
            public readonly string Group;
            public readonly ushort Index;

            /// <summary>
            /// Initializes a new instance of the <see cref="ZoneUIData"/> struct.
            /// </summary>
            public ZoneUIData(string name, string thumbnail, string group, ushort index) {
                Name = name;
                Thumbnail = thumbnail;
                Group = group;
                Index = index;
            }

            /// <inheritdoc/>
            public readonly void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("name");
                writer.Write(Name);

                writer.PropertyName("thumbnail");
                writer.Write(Thumbnail);

                writer.PropertyName("group");
                writer.Write(Thumbnail);

                writer.PropertyName("index");
                writer.Write(Index);

                writer.TypeEnd();
            }
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_UISystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_Query = GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<UIAssetMenuData>(),
            });

            // Systems
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_PlatterOverlaySystem = World.GetOrCreateSystemManaged<P_OverlaySystem>();
            m_ParcelSpawnSystem = World.GetOrCreateSystemManaged<P_ParcelSpawnSystem>();
            m_SnapSystem = World.GetOrCreateSystemManaged<P_SnapSystem>();
            m_ZoneCacheSystem = World.GetOrCreateSystemManaged<P_ZoneCacheSystem>();

            // Bindings
            m_EnableToolButtonsBinding = CreateBinding<bool>("ENABLE_TOOL_BUTTONS", false);
            m_ZoneBinding = CreateBinding<int>("ZONE", 0, SetPreZone);
            m_BlockWidthBinding = CreateBinding<int>("BLOCK_WIDTH", 2);
            m_BlockDepthBinding = CreateBinding<int>("BLOCK_DEPTH", 2);
            m_ZoneDataBinding = CreateBinding<ZoneUIData[]>("ZONE_DATA", new ZoneUIData[] { });
            m_RenderParcelsBinding = CreateBinding<bool>("RENDER_PARCELS", true, SetRenderParcels);
            m_AllowSpawningBinding = CreateBinding<bool>("ALLOW_SPAWNING", true, SetAllowSpawning);
            m_SnapRoadSideBinding = CreateBinding<bool>("SNAP_ROADSIDE", true, SetSnapRoadside);
            m_SnapSpacingBinding = CreateBinding<float>("SNAP_SPACING", P_SnapSystem.DEFAULT_SNAP_DISTANCE, SetSnapSpacing);

            // Triggers
            CreateTrigger<string>("ADJUST_BLOCK_SIZE", HandleBlockSizeAdjustment);

            // Shortcuts
            m_IncreaseBlockWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelWidthActionName);
            m_IncreaseBlockDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelDepthActionName);
            m_DecreaseBlockWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelWidthActionName);
            m_DecreaseBlockDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelDepthActionName);
            m_ToggleRender = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.ToggleRenderActionName);
            m_ToggleSpawn = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.ToggleSpawnActionName);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var currentlyUsingParcelsInObjectTool = CurrentlyUsingParcelsInObjectTool();

            // Enable Tool Shortcuts
            m_IncreaseBlockWidthAction.shouldBeEnabled = currentlyUsingParcelsInObjectTool;
            m_IncreaseBlockDepthAction.shouldBeEnabled = currentlyUsingParcelsInObjectTool;
            m_DecreaseBlockWidthAction.shouldBeEnabled = currentlyUsingParcelsInObjectTool;
            m_DecreaseBlockDepthAction.shouldBeEnabled = currentlyUsingParcelsInObjectTool;

            // Handle Shortcuts
            if (m_IncreaseBlockWidthAction.WasPerformedThisFrame()) {
                IncreaseBlockWidth();
            }

            if (m_IncreaseBlockDepthAction.WasPerformedThisFrame()) {
                IncreaseBlockDepth();
            }

            if (m_DecreaseBlockWidthAction.WasPerformedThisFrame()) {
                DecreaseBlockWidth();
            }

            if (m_DecreaseBlockDepthAction.WasPerformedThisFrame()) {
                DecreaseBlockDepth();
            }

            if (m_ToggleRender.WasPerformedThisFrame()) {
                ToggleRenderParcels();
            }

            if (m_ToggleSpawn.WasPerformedThisFrame()) {
                ToggleAllowSpawning();
            }

            // Make sure we refresh the lot sizes if the Object Tool is active
            if (currentlyUsingParcelsInObjectTool) {
                m_SelectedParcelSize.x = ((ParcelPrefab)m_ObjectToolSystem.prefab).m_LotWidth;
                m_SelectedParcelSize.y = ((ParcelPrefab)m_ObjectToolSystem.prefab).m_LotDepth;
            }

            // Rendering should be on when using the tool
            m_PlatterOverlaySystem.RenderParcelsOverride = currentlyUsingParcelsInObjectTool;

            // Update Bindings
            m_RenderParcelsBinding.Value = m_PlatterOverlaySystem.RenderParcels;
            m_BlockWidthBinding.Value = m_SelectedParcelSize.x;
            m_BlockDepthBinding.Value = m_SelectedParcelSize.y;
            m_EnableToolButtonsBinding.Value = currentlyUsingParcelsInObjectTool;

            // Send down zone data when ready
            // todo refresh!
            if (m_ZoneDataBinding.Value.Length == 0) {
                var zoneData = m_ZoneCacheSystem.ZoneUIData.Values.ToArray();
                Array.Sort(zoneData, (x, y) => x.Index.CompareTo(y.Index));
                m_ZoneDataBinding.Value = zoneData;
            }
        }

        /// <inheritdoc/>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);
            m_Log.Debug($"OnGameLoadingComplete(purpose={purpose}, mode={mode})");

            m_ToggleRender.shouldBeEnabled = mode.IsGameOrEditor();
            m_ToggleSpawn.shouldBeEnabled = mode.IsGameOrEditor();
        }

        /// <summary>
        /// </summary>
        private bool CurrentlyUsingParcelsInObjectTool() {
            return m_ToolSystem.activeTool is ObjectToolSystem && m_ObjectToolSystem.prefab is ParcelPrefab;
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
        private void ToggleRenderParcels() {
            m_Log.Debug($"ToggleRenderParcels()");
            SetRenderParcels(!m_PlatterOverlaySystem.RenderParcels);
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
        private void ToggleAllowSpawning() {
            m_Log.Debug($"ToggleAllowSpawning()");
            SetAllowSpawning(!AllowSpawning);
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetAllowSpawning(bool enabled) {
            m_Log.Debug($"SetAllowSpawning(enabled = {enabled})");
            AllowSpawning = enabled;
            m_ParcelSpawnSystem.UpdateSpawning(enabled);
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetSnapRoadside(bool enabled) {
            m_Log.Debug($"SetSnapRoadside(enabled = {enabled})");
            if (enabled) {
                m_SnapSystem.CurrentSnapMode |= P_SnapSystem.SnapMode.RoadSide;
            } else {
                m_SnapSystem.CurrentSnapMode &= ~P_SnapSystem.SnapMode.RoadSide;
            }
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetSnapSpacing(float amount) {
            m_Log.Debug($"SetSnapRoadside(enabled = {amount})");
            if (amount < 1f) {
                amount = 1f;
            } else if (amount >= P_SnapSystem.MAX_SNAP_DISTANCE) {
                amount = 100f;
            }

            m_SnapSystem.CurrentSnapOffset = amount;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetPreZone(int zoneIndex) {
            m_Log.Debug($"SetPreZone(modeIndex = {zoneIndex})");
            var zonePrefab = m_ZoneCacheSystem.ZonePrefabs[zoneIndex];
            var zoneData = EntityManager.GetComponentData<ZoneData>(zonePrefab);
            PreZoneType = zoneData.m_ZoneType;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void DecreaseBlockWidth() {
            if (m_SelectedParcelSize.x > P_PrefabsCreateSystem.BlockSizes.x) {
                m_SelectedParcelSize.x -= 1;
            }

            m_Log.Debug("DecreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void IncreaseBlockWidth() {
            if (m_SelectedParcelSize.x < P_PrefabsCreateSystem.BlockSizes.z) {
                m_SelectedParcelSize.x += 1;
            }

            m_Log.Debug("IncreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void DecreaseBlockDepth() {
            if (m_SelectedParcelSize.y > P_PrefabsCreateSystem.BlockSizes.y) {
                m_SelectedParcelSize.y -= 1;
            }

            m_Log.Debug("DecreaseBlockDepth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void IncreaseBlockDepth() {
            if (m_SelectedParcelSize.y < P_PrefabsCreateSystem.BlockSizes.w) {
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
