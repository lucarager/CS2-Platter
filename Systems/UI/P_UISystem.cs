// <copyright file="P_UISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using System.Linq;
    using Colossal.Serialization.Entities;
    using Colossal.UI.Binding;
    using Game;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Platter;
    using Platter.Extensions;
    using Platter.Settings;
    using Platter.Systems;
    using Platter.Utils;
    using Unity.Mathematics;

    /// <summary>
    /// System responsible for UI Bindings & Data Handling.
    /// </summary>
    public partial class P_UISystem : ExtendedUISystemBase {
        public ZoneType PreZoneType { get; private set; } = ZoneType.None;

        public bool AllowSpawning { get; private set; } = true;

        // Systems
        private PrefabSystem        m_PrefabSystem;
        private ToolSystem          m_ToolSystem;
        private ObjectToolSystem    m_ObjectToolSystem;
        private P_OverlaySystem     m_PlatterOverlaySystem;
        private P_AllowSpawnSystem  m_AllowSpawnSystem;
        private P_SnapSystem        m_SnapSystem;
        private P_ZoneCacheSystem   m_ZoneCacheSystem;

        // Logger
        private PrefixedLogger m_Log;

        // Data
        private int2 m_SelectedParcelSize = new(2, 2);

        // Bindings
        private ValueBindingHelper<bool>         m_EnableToolButtonsBinding;
        private ValueBindingHelper<int>          m_ZoneBinding;
        private ValueBindingHelper<ZoneUIData[]> m_ZoneDataBinding;
        private ValueBindingHelper<int>          m_BlockWidthBinding;
        private ValueBindingHelper<int>          m_BlockDepthBinding;
        private ValueBindingHelper<bool>         m_RenderParcelsBinding;
        private ValueBindingHelper<bool>         m_AllowSpawningBinding;
        private ValueBindingHelper<bool>         m_SnapRoadSideBinding;
        private ValueBindingHelper<float>        m_SnapSpacingBinding;
        private ValueBindingHelper<bool>         m_ModalFirstLaunchBinding;

        // Shortcuts
        private ProxyAction m_IncreaseBlockWidthAction;
        private ProxyAction m_IncreaseBlockDepthAction;
        private ProxyAction m_DecreaseBlockWidthAction;
        private ProxyAction m_DecreaseBlockDepthAction;
        private ProxyAction m_ToggleRender;
        private ProxyAction m_ToggleSpawn;

        /// <summary>
        /// Struct to store and send Zone Data and to the React UI.
        /// </summary>
        public readonly struct ZoneUIData : IJsonWritable {
            public readonly string Name;
            public readonly string Thumbnail;
            public readonly string Category;
            public readonly ushort Index;

            /// <summary>
            /// Initializes a new instance of the <see cref="ZoneUIData"/> struct.
            /// </summary>
            public ZoneUIData(string name,
                              string thumbnail,
                              string category,
                              ushort index) {
                Name      = name;
                Thumbnail = thumbnail;
                Category  = category;
                Index     = index;
            }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("name");
                writer.Write(Name);

                writer.PropertyName("thumbnail");
                writer.Write(Thumbnail);

                writer.PropertyName("category");
                writer.Write(Category);

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
            m_Log.Debug("OnCreate()");

            // Systems
            m_ToolSystem           = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem         = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ObjectToolSystem     = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_PlatterOverlaySystem = World.GetOrCreateSystemManaged<P_OverlaySystem>();
            m_AllowSpawnSystem     = World.GetOrCreateSystemManaged<P_AllowSpawnSystem>();
            m_SnapSystem           = World.GetOrCreateSystemManaged<P_SnapSystem>();
            m_ZoneCacheSystem      = World.GetOrCreateSystemManaged<P_ZoneCacheSystem>();

            // Bindings
            m_EnableToolButtonsBinding = CreateBinding("ENABLE_TOOL_BUTTONS", false);
            m_ZoneBinding              = CreateBinding("ZONE", 0, SetPreZone);
            m_BlockWidthBinding        = CreateBinding("BLOCK_WIDTH", 2);
            m_BlockDepthBinding        = CreateBinding("BLOCK_DEPTH", 2);
            m_ZoneDataBinding          = CreateBinding("ZONE_DATA", new ZoneUIData[] { });
            m_RenderParcelsBinding     = CreateBinding("RENDER_PARCELS", true, SetRenderParcels);
            m_AllowSpawningBinding     = CreateBinding("ALLOW_SPAWNING", true, SetAllowSpawning);
            m_SnapRoadSideBinding      = CreateBinding("SNAP_ROADSIDE", true, SetSnapRoadside);
            m_SnapSpacingBinding       = CreateBinding("SNAP_SPACING", P_SnapSystem.DefaultSnapDistance, SetSnapSpacing);
            m_ModalFirstLaunchBinding  = CreateBinding("MODAL__FIRST_LAUNCH", PlatterMod.Instance.Settings.ModalFirstLaunch);

            // Triggers
            CreateTrigger<string>("ADJUST_BLOCK_SIZE", HandleBlockSizeAdjustment);
            CreateTrigger<string>("MODAL_DISMISS", HandleModalDismiss);

            // Shortcuts
            m_IncreaseBlockWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelWidthActionName);
            m_IncreaseBlockDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelDepthActionName);
            m_DecreaseBlockWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelWidthActionName);
            m_DecreaseBlockDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelDepthActionName);
            m_ToggleRender             = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.ToggleRenderActionName);
            m_ToggleSpawn              = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.ToggleSpawnActionName);
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
            m_RenderParcelsBinding.Value     = m_PlatterOverlaySystem.RenderParcels;
            m_BlockWidthBinding.Value        = m_SelectedParcelSize.x;
            m_BlockDepthBinding.Value        = m_SelectedParcelSize.y;
            m_EnableToolButtonsBinding.Value = currentlyUsingParcelsInObjectTool;
            m_ModalFirstLaunchBinding.Value  = PlatterMod.Instance.Settings.ModalFirstLaunch;

            // Send down zone data
            var zoneData = m_ZoneCacheSystem.ZoneUIData.Values.ToArray();
            Array.Sort(zoneData, (x, y) => x.Index.CompareTo(y.Index));
            m_ZoneDataBinding.Value = zoneData;
        }

        /// <inheritdoc/>
        protected override void OnGameLoadingComplete(Purpose  purpose,
                                                      GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);
            m_Log.Debug($"OnGameLoadingComplete(purpose={purpose}, mode={mode})");

            m_ToggleRender.shouldBeEnabled = mode.IsGameOrEditor();
            m_ToggleSpawn.shouldBeEnabled  = mode.IsGameOrEditor();
        }

        /// <summary>
        /// </summary>
        private bool CurrentlyUsingParcelsInObjectTool() {
            return m_ToolSystem.activeTool is ObjectToolSystem && m_ObjectToolSystem.prefab is ParcelPrefab;
        }

        /// <summary>
        /// Open the panel.
        /// </summary>
        private void HandleModalDismiss(string modal) {
            m_Log.Debug($"HandleModalDismiss(modal: {modal})");
            PlatterMod.Instance.Settings.ModalFirstLaunch = true;
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
            }
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void ToggleRenderParcels() {
            m_Log.Debug("ToggleRenderParcels()");
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
            m_Log.Debug("ToggleAllowSpawning()");
            SetAllowSpawning(!AllowSpawning);
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetAllowSpawning(bool enabled) {
            m_Log.Debug($"SetAllowSpawning(enabled = {enabled})");
            AllowSpawning = enabled;
            m_AllowSpawnSystem.UpdateSpawning(enabled);
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
            } else if (amount >= P_SnapSystem.MaxSnapDistance) {
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
            var zoneData   = EntityManager.GetComponentData<ZoneData>(zonePrefab);
            PreZoneType = zoneData.m_ZoneType;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void DecreaseBlockWidth() {
            if (m_SelectedParcelSize.x > P_PrefabsCreateSystem.BlockSizes.x) {
                m_SelectedParcelSize.x -= 1;
            }

            m_Log.Debug("DecreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        ///  Called from the UI.
        /// </summary>
        private void IncreaseBlockWidth() {
            if (m_SelectedParcelSize.x < P_PrefabsCreateSystem.BlockSizes.z) {
                m_SelectedParcelSize.x += 1;
            }

            m_Log.Debug("IncreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        ///  Called from the UI.
        /// </summary>
        private void DecreaseBlockDepth() {
            if (m_SelectedParcelSize.y > P_PrefabsCreateSystem.BlockSizes.y) {
                m_SelectedParcelSize.y -= 1;
            }

            m_Log.Debug("DecreaseBlockDepth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        ///  Called from the UI.
        /// </summary>
        private void IncreaseBlockDepth() {
            if (m_SelectedParcelSize.y < P_PrefabsCreateSystem.BlockSizes.w) {
                m_SelectedParcelSize.y += 1;
            }

            m_Log.Debug("IncreaseBlockDepth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        ///  Called from the UI.
        /// </summary>
        private void UpdateSelectedPrefab() {
            var id = ParcelUtils.CreatePrefabID(m_SelectedParcelSize);

            m_Log.Debug($"UpdateSelectedPrefab() -- Attempting to get Prefab with id {id}");

            if (!m_PrefabSystem.TryGetPrefab(id, out var prefabBase)) {
                m_Log.Debug("UpdateSelectedPrefab() -- Couldn't find prefabBase!");
                return;
            }

            m_Log.Debug($"UpdateSelectedPrefab() -- Found ${prefabBase}!");

            m_ToolSystem.ActivatePrefabTool((StaticObjectPrefab)prefabBase);
        }
    }
}