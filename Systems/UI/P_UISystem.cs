// <copyright file="P_UISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using System.Linq;
    using System.Reflection;
    using Colossal.Serialization.Entities;
    using Colossal.UI.Binding;
    using Extensions;
    using Game;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI.InGame;
    using Game.Zones;
    using Settings;
    using Unity.Mathematics;
    using Utils;
    using static P_SnapSystem;

    #endregion

    /// <summary>
    /// System responsible for UI Bindings & Data Handling.
    /// </summary>
    public partial class P_UISystem : ExtendedUISystemBase {
        public enum PlatterToolMode {
            Plop     = 0,
            RoadEdge = 1,
        }
        public ZoneType PreZoneType      { get; set; } = ZoneType.None;
        public bool     ShowContourLines { get; set; } = false;
        public bool     ShowZones        { get; set; } = false;

        private int2                             m_SelectedParcelSize = new(2, 2);
        private ObjectToolSystem                 m_ObjectToolSystem;
        private P_AllowSpawnSystem               m_AllowSpawnSystem;
        private P_OverlaySystem                  m_PlatterOverlaySystem;
        private P_SnapSystem                     m_SnapSystem;
        private P_ZoneCacheSystem                m_ZoneCacheSystem;
        private PrefabSystem                     m_PrefabSystem;
        private PrefixedLogger                   m_Log;
        private ProxyAction                      m_DecreaseBlockDepthAction;
        private ProxyAction                      m_DecreaseBlockWidthAction;
        private ProxyAction                      m_IncreaseBlockDepthAction;
        private ProxyAction                      m_IncreaseBlockWidthAction;
        private ProxyAction                      m_ToggleRender;
        private ProxyAction                      m_ToggleSpawn;
        private ToolbarUISystem                  m_ToolbarUISystem;
        private ToolSystem                       m_ToolSystem;
        private ValueBindingHelper<bool>         m_AllowSpawningBinding;
        private ValueBindingHelper<bool>         m_EnableCreateFromZoneBinding;
        private ValueBindingHelper<bool>         m_EnableToolButtonsBinding;
        private ValueBindingHelper<bool>         m_ModalFirstLaunchBinding;
        private ValueBindingHelper<bool>         m_RenderParcelsBinding;
        private ValueBindingHelper<bool>         m_ShowContourLinesBinding;
        private ValueBindingHelper<bool>         m_ShowZonesBinding;
        private ValueBindingHelper<float>        m_SnapSpacingBinding;
        private ValueBindingHelper<int>          m_BlockDepthBinding;
        private ValueBindingHelper<int>          m_BlockDepthMaxBinding;
        private ValueBindingHelper<int>          m_BlockDepthMinBinding;
        private ValueBindingHelper<int>          m_BlockWidthBinding;
        private ValueBindingHelper<int>          m_BlockWidthMaxBinding;
        private ValueBindingHelper<int>          m_BlockWidthMinBinding;
        private ValueBindingHelper<int>          m_SnapModeBinding;
        private ValueBindingHelper<int>          m_ToolModeBinding;
        private ValueBindingHelper<int>          m_ZoneBinding;
        private ValueBindingHelper<ZoneUIData[]> m_ZoneDataBinding;

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
            m_ToolbarUISystem      = World.GetOrCreateSystemManaged<ToolbarUISystem>();
            m_AllowSpawnSystem     = World.GetOrCreateSystemManaged<P_AllowSpawnSystem>();
            m_SnapSystem           = World.GetOrCreateSystemManaged<P_SnapSystem>();
            m_ZoneCacheSystem      = World.GetOrCreateSystemManaged<P_ZoneCacheSystem>();

            // Bindings
            m_EnableToolButtonsBinding    = CreateBinding("ENABLE_TOOL_BUTTONS", false);
            m_EnableCreateFromZoneBinding = CreateBinding("ENABLE_CREATE_FROM_ZONE", false);
            m_ZoneBinding                 = CreateBinding("ZONE", 0, SetPreZone);
            m_BlockWidthBinding           = CreateBinding("BLOCK_WIDTH", 2);
            m_BlockWidthMinBinding        = CreateBinding("BLOCK_WIDTH_MIN", P_PrefabsCreateSystem.BlockSizes.x);
            m_BlockWidthMaxBinding        = CreateBinding("BLOCK_WIDTH_MAX", P_PrefabsCreateSystem.BlockSizes.z);
            m_BlockDepthBinding           = CreateBinding("BLOCK_DEPTH", 2);
            m_BlockDepthMinBinding        = CreateBinding("BLOCK_DEPTH_MIN", P_PrefabsCreateSystem.BlockSizes.y);
            m_BlockDepthMaxBinding        = CreateBinding("BLOCK_DEPTH_MAX", P_PrefabsCreateSystem.BlockSizes.w);
            m_ZoneDataBinding             = CreateBinding("ZONE_DATA", new ZoneUIData[] { });
            m_RenderParcelsBinding        = CreateBinding("RENDER_PARCELS", PlatterMod.Instance.Settings.RenderParcels, SetRenderParcels);
            m_AllowSpawningBinding        = CreateBinding("ALLOW_SPAWNING", PlatterMod.Instance.Settings.AllowSpawn, SetAllowSpawning);
            m_ShowContourLinesBinding     = CreateBinding("SHOW_CONTOUR_LINES", false, SetShowContourLines);
            m_ShowZonesBinding            = CreateBinding("SHOW_ZONES", false, SetShowZones);
            m_SnapModeBinding             = CreateBinding("SNAP_MODE", (int)m_SnapSystem.CurrentSnapMode, SetSnapMode);
            m_SnapSpacingBinding          = CreateBinding("SNAP_SPACING", DefaultSnapDistance, SetSnapSpacing);
            m_ModalFirstLaunchBinding     = CreateBinding("MODAL__FIRST_LAUNCH", PlatterMod.Instance.Settings.Modals_FirstLaunchTutorial);
            m_ToolModeBinding             = CreateBinding("TOOL_MODE", 0, SetToolMode);

            // Triggers
            CreateTrigger<string>("ADJUST_BLOCK_SIZE", HandleBlockSizeAdjustment);
            CreateTrigger<string>("MODAL_DISMISS", HandleModalDismiss);
            CreateTrigger("CREATE_PARCEL_WITH_ZONE", HandleCreateParcelWithZone);

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
            var currentlyUsingParcelsInObjectTool = m_ToolSystem.activePrefab is ParcelPlaceholderPrefab;
            var currentlyUsingZoneTool            = m_ToolSystem.activePrefab is ZonePrefab && m_ToolSystem.activeTool is ZoneToolSystem;

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
                m_SelectedParcelSize.x = ((ParcelPlaceholderPrefab)m_ObjectToolSystem.prefab).m_LotWidth;
                m_SelectedParcelSize.y = ((ParcelPlaceholderPrefab)m_ObjectToolSystem.prefab).m_LotDepth;
            }

            // Rendering should be on when using a tool
            m_PlatterOverlaySystem.RenderParcelsOverride = ShouldRenderOverlay();

            // Update Bindings
            m_RenderParcelsBinding.Value        = PlatterMod.Instance.Settings.RenderParcels;
            m_AllowSpawningBinding.Value        = PlatterMod.Instance.Settings.AllowSpawn;
            m_BlockWidthBinding.Value           = m_SelectedParcelSize.x;
            m_BlockDepthBinding.Value           = m_SelectedParcelSize.y;
            m_EnableToolButtonsBinding.Value    = currentlyUsingParcelsInObjectTool;
            m_EnableCreateFromZoneBinding.Value = currentlyUsingZoneTool;
            m_ModalFirstLaunchBinding.Value     = PlatterMod.Instance.Settings.Modals_FirstLaunchTutorial;
            m_ZoneBinding.Value                 = PreZoneType.m_Index;
            m_ShowContourLinesBinding.Value     = ShowContourLines;
            m_ShowZonesBinding.Value            = ShowZones;

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
        private bool ShouldRenderOverlay() {
            return m_ToolSystem.activeTool is ObjectToolSystem or BulldozeToolSystem or NetToolSystem or ZoneToolSystem ||
                   m_ToolSystem.activePrefab is ParcelPlaceholderPrefab;
        }

        /// <summary>
        /// Open the panel.
        /// </summary>
        private void HandleModalDismiss(string modal) {
            m_Log.Debug($"HandleModalDismiss(modal: {modal})");
            PlatterMod.Instance.Settings.Modals_FirstLaunchTutorial = true;
        }

        /// <summary>
        /// HandleCreateParcelWithZone
        /// </summary>
        private void HandleCreateParcelWithZone() {
            m_Log.Debug("HandleCreateParcelWithZone()");

            var currentZonePrefab = (ZonePrefab)m_ToolSystem.activeTool.GetPrefab();
            var entity            = m_PrefabSystem.GetEntity(currentZonePrefab);
            var zoneData          = EntityManager.GetComponentData<ZoneData>(entity);

            PreZoneType = zoneData.m_ZoneType;

            UpdateSelectedPrefab();
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
            SetRenderParcels(!PlatterMod.Instance.Settings.RenderParcels);
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetRenderParcels(bool enabled) {
            m_Log.Debug($"SetRenderParcels(enabled = {enabled})");
            PlatterMod.Instance.Settings.RenderParcels = enabled;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void ToggleAllowSpawning() {
            m_Log.Debug("ToggleAllowSpawning()");
            SetAllowSpawning(!PlatterMod.Instance.Settings.AllowSpawn);
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetAllowSpawning(bool enabled) {
            m_Log.Debug($"SetAllowSpawning(enabled = {enabled})");
            PlatterMod.Instance.Settings.AllowSpawn = enabled;
            m_AllowSpawnSystem.UpdateSpawning(enabled);
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetShowContourLines(bool enabled) {
            m_Log.Debug($"SetShowContourLines(enabled = {enabled})");
            ShowContourLines = enabled;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetShowZones(bool enabled) {
            m_Log.Debug($"SetShowZones(enabled = {enabled})");
            ShowZones = enabled;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetSnapMode(int snapMode) {
            m_Log.Debug($"SetSnapZoneside(snapMode = {snapMode})");
            m_SnapSystem.CurrentSnapMode = (SnapMode)snapMode;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetSnapRoadside(bool enabled) {
            m_Log.Debug($"SetSnapRoadside(enabled = {enabled})");
            if (enabled) {
                m_SnapSystem.CurrentSnapMode |= SnapMode.RoadSide;
            } else {
                m_SnapSystem.CurrentSnapMode &= ~SnapMode.RoadSide;
            }
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetSnapSpacing(float amount) {
            m_Log.Debug($"SetSnapSpacing(amount = {amount})");
            if (amount < MinSnapDistance) {
                amount = MinSnapDistance;
            } else if (amount > MaxSnapDistance) {
                amount = MaxSnapDistance;
            }

            m_SnapSystem.CurrentSnapSetback = amount;
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetPreZone(int zoneIndex) {
            m_Log.Debug($"SetPreZone(modeIndex = {zoneIndex})");
            var zonePrefab = m_ZoneCacheSystem.ZonePrefabs[(ushort)zoneIndex];
            var zoneData   = EntityManager.GetComponentData<ZoneData>(zonePrefab);
            PreZoneType = zoneData.m_ZoneType;
            UpdateSelectedPrefab();
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
            var id = ParcelUtils.GetPrefabID(m_SelectedParcelSize, true);
            m_Log.Debug($"UpdateSelectedPrefab() -- ${id}!");

            if (!m_PrefabSystem.TryGetPrefab(id, out var prefabBase)) {
                return;
            }

            var assetEntity = m_PrefabSystem.GetEntity(prefabBase);
            m_ToolSystem.ActivatePrefabTool(prefabBase);

            // Invoke the private SelectAsset method using reflection
            var method = m_ToolbarUISystem.GetType().GetMethod(
                "SelectAsset",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method != null) {
                method.Invoke(m_ToolbarUISystem, new object[] { assetEntity, true });
            }
        }

        /// <summary>
        /// Called from the UI.
        /// </summary>
        private void SetToolMode(int modeIndex) {
            m_Log.Debug($"SetToolMode(modeIndex = {modeIndex})");
            var mode = (PlatterToolMode)modeIndex;

            switch (mode) {
                case PlatterToolMode.Plop:
                    return;
                case PlatterToolMode.RoadEdge: {
                    //if (!m_RoadsideToolSystem.Enabled) {
                    //    m_RoadsideToolSystem.RequestEnable();
                    //}

                    break;
                }
            }
        }

        /// <summary>
        /// Struct to store and send Zone Data and to the React UI.
        /// </summary>
        public readonly struct ZoneUIData : IJsonWritable {
            public readonly string Name;
            public readonly string Thumbnail;
            public readonly string Category;
            public readonly int    Index;

            /// <summary>
            /// Initializes a new instance of the <see cref="ZoneUIData"/> struct.
            /// </summary>
            public ZoneUIData(string name,
                              string thumbnail,
                              string category,
                              int    index) {
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
    }
}