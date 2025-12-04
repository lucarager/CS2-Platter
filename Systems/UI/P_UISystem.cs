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
    using ZoneUIDataModel = P_ZoneCacheSystem.ZoneUIDataModel;
    using AssetPackUIDataModel = P_ZoneCacheSystem.AssetPackUIDataModel;

    #endregion

    /// <summary>
    /// System responsible for UI Bindings & Data Handling.
    /// </summary>
    public partial class P_UISystem : ExtendedUISystemBase {
        public enum PlatterToolMode {
            Plop     = 0,
            RoadEdge = 1,
        }

        private int2                             m_SelectedParcelSize = new(2, 2);
        private ObjectToolSystem                 m_ObjectToolSystem;
        private P_AllowSpawnSystem               m_AllowSpawnSystem;
        private P_OverlaySystem                  m_PlatterOverlaySystem;
        private P_SnapSystem                     m_SnapSystem;
        private P_ZoneCacheSystem                m_ZoneCacheSystem;
        private PrefabSystem                     m_PrefabSystem;
        private PrefixedLogger                   m_Log;
        private ProxyAction                      m_BlockDepthAction;
        private ProxyAction                      m_BlockSizeAction;
        private ProxyAction                      m_BlockWidthAction;
        private ProxyAction                      m_OpenPlatterPanel;
        private ProxyAction                      m_SetbackAction;
        private ProxyAction                      m_ToggleRender;
        private ProxyAction                      m_ToggleSpawn;
        private ToolbarUISystem                  m_ToolbarUISystem;
        private ToolSystem                       m_ToolSystem;
        private ValueBindingHelper<bool>         m_AllowSpawningBinding;
        private ValueBindingHelper<bool>         m_EnableCreateFromZoneBinding;
        private ValueBindingHelper<bool>         m_EnableSnappingOptionsBinding;
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
        private ValueBindingHelper<ZoneUIDataModel[]> m_ZoneDataBinding;
        private ValueBindingHelper<AssetPackUIDataModel[]> m_AssetPackDataBinding;
        private ValueBindingHelper<float>        m_MaxSnapSpacingBinding;
        private bool                             CurrentlyUsingParcelsInObjectTool => m_ToolSystem.activePrefab is ParcelPlaceholderPrefab;
        private bool                             CurrentlyUsingZoneTool => m_ToolSystem.activePrefab is ZonePrefab && m_ToolSystem.activeTool is ZoneToolSystem;

        public bool                             ShowContourLines { get; set; }
        public  bool                             ShowZones { get; set; }
        public  ZoneType                         PreZoneType { get; set; } = ZoneType.None;

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
            m_EnableToolButtonsBinding     = CreateBinding("ENABLE_TOOL_BUTTONS", false);
            m_EnableSnappingOptionsBinding = CreateBinding("ENABLE_SNAPPING_OPTIONS", false);
            m_EnableCreateFromZoneBinding  = CreateBinding("ENABLE_CREATE_FROM_ZONE", false);
            m_ZoneBinding                  = CreateBinding("ZONE", 0, SetPreZone);
            m_BlockWidthBinding            = CreateBinding("BLOCK_WIDTH", 2);
            m_BlockWidthMinBinding         = CreateBinding("BLOCK_WIDTH_MIN", P_PrefabsCreateSystem.BlockSizes.x);
            m_BlockWidthMaxBinding         = CreateBinding("BLOCK_WIDTH_MAX", P_PrefabsCreateSystem.BlockSizes.z);
            m_BlockDepthBinding            = CreateBinding("BLOCK_DEPTH", 2);
            m_BlockDepthMinBinding         = CreateBinding("BLOCK_DEPTH_MIN", P_PrefabsCreateSystem.BlockSizes.y);
            m_BlockDepthMaxBinding         = CreateBinding("BLOCK_DEPTH_MAX", P_PrefabsCreateSystem.BlockSizes.w);
            m_ZoneDataBinding              = CreateBinding("ZONE_DATA", new ZoneUIDataModel[] { });
            m_AssetPackDataBinding         = CreateBinding("ASSET_PACK_DATA", new AssetPackUIDataModel[] { });
            m_RenderParcelsBinding         = CreateBinding("RENDER_PARCELS", PlatterMod.Instance.Settings.RenderParcels, SetRenderParcels);
            m_AllowSpawningBinding         = CreateBinding("ALLOW_SPAWNING", PlatterMod.Instance.Settings.AllowSpawn, SetAllowSpawning);
            m_ShowContourLinesBinding      = CreateBinding("SHOW_CONTOUR_LINES", false, SetShowContourLines);
            m_ShowZonesBinding             = CreateBinding("SHOW_ZONES", false, SetShowZones);
            m_SnapModeBinding              = CreateBinding("SNAP_MODE", (int)m_SnapSystem.CurrentSnapMode, SetSnapMode);
            m_SnapSpacingBinding           = CreateBinding("SNAP_SPACING", DefaultSnapDistance, SetSnapSpacing);
            m_MaxSnapSpacingBinding        = CreateBinding("MAX_SNAP_SPACING", MaxSnapDistance);
            m_ModalFirstLaunchBinding      = CreateBinding("MODAL__FIRST_LAUNCH", PlatterMod.Instance.Settings.Modals_FirstLaunchTutorial);
            m_ToolModeBinding              = CreateBinding("TOOL_MODE", 0, SetToolMode);

            // Triggers
            CreateTrigger<string>("ADJUST_BLOCK_SIZE", HandleBlockSizeAdjustment);
            CreateTrigger<string>("MODAL_DISMISS", HandleModalDismiss);
            CreateTrigger("CREATE_PARCEL_WITH_ZONE", HandleCreateParcelWithZone);

            // Shortcuts
            m_ToggleRender     = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.ToggleRenderName);
            m_ToggleSpawn      = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.ToggleSpawnName);
            m_OpenPlatterPanel = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.OpenPanelName);
            m_BlockWidthAction = InputManager.instance.FindAction("Platter.Platter.PlatterMod", "BlockWidthAction");
            m_BlockDepthAction = InputManager.instance.FindAction("Platter.Platter.PlatterMod", "BlockDepthAction");
            m_BlockSizeAction  = InputManager.instance.FindAction("Platter.Platter.PlatterMod", "BlockSizeAction");
            m_SetbackAction    = InputManager.instance.FindAction("Platter.Platter.PlatterMod", "SetbackAction");

            // Always enable
            m_OpenPlatterPanel.shouldBeEnabled = true;
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // Enable Tool Shortcuts
            m_BlockWidthAction.shouldBeEnabled = CurrentlyUsingParcelsInObjectTool;
            m_BlockDepthAction.shouldBeEnabled = CurrentlyUsingParcelsInObjectTool;
            m_BlockSizeAction.shouldBeEnabled  = CurrentlyUsingParcelsInObjectTool;
            m_SetbackAction.shouldBeEnabled    = CurrentlyUsingParcelsInObjectTool;

            // Handle Shortcuts
            HandleProxyActions();

            // Make sure we refresh the lot sizes if the Object Tool is active
            if (CurrentlyUsingParcelsInObjectTool) {
                m_SelectedParcelSize.x = ((ParcelPlaceholderPrefab)m_ObjectToolSystem.prefab).m_LotWidth;
                m_SelectedParcelSize.y = ((ParcelPlaceholderPrefab)m_ObjectToolSystem.prefab).m_LotDepth;
            }

            // Rendering should be on when using a tool
            m_PlatterOverlaySystem.RenderParcelsOverride = ShouldRenderOverlay();

            // Update Bindings
            UpdateBindings();
        }

        private void UpdateBindings() {
            m_RenderParcelsBinding.Value     = PlatterMod.Instance.Settings.RenderParcels;
            m_AllowSpawningBinding.Value     = PlatterMod.Instance.Settings.AllowSpawn;
            m_BlockWidthBinding.Value        = m_SelectedParcelSize.x;
            m_BlockDepthBinding.Value        = m_SelectedParcelSize.y;
            m_EnableToolButtonsBinding.Value = CurrentlyUsingParcelsInObjectTool;
            m_EnableSnappingOptionsBinding.Value = m_ObjectToolSystem.actualMode is not ObjectToolSystem.Mode.Create or ObjectToolSystem.Mode.Line
                or ObjectToolSystem.Mode.Curve or ObjectToolSystem.Mode.Curve;
            m_EnableCreateFromZoneBinding.Value = CurrentlyUsingZoneTool;
            m_ModalFirstLaunchBinding.Value     = PlatterMod.Instance.Settings.Modals_FirstLaunchTutorial;
            m_ZoneBinding.Value                 = PreZoneType.m_Index;
            m_ShowContourLinesBinding.Value     = ShowContourLines;
            m_ShowZonesBinding.Value            = ShowZones;
            m_SnapSpacingBinding.Value          = m_SnapSystem.CurrentSnapSetback;
            var zoneData = m_ZoneCacheSystem.ZoneUIData.Values.ToArray();
            Array.Sort(zoneData, (x, y) => x.Index.CompareTo(y.Index));
            m_ZoneDataBinding.Value = zoneData;
            m_AssetPackDataBinding.Value = m_ZoneCacheSystem.AssetPackUIData.ToArray();
        }

        private void HandleProxyActions() {
            if (m_ToggleRender.WasPerformedThisFrame()) {
                ToggleRenderParcels();
            }

            if (m_ToggleSpawn.WasPerformedThisFrame()) {
                ToggleAllowSpawning();
            }

            if (m_OpenPlatterPanel.WasPerformedThisFrame()) {
                OpenPlatterPanel();
            }

            if (m_BlockWidthAction.IsInProgress()) {
                var scrollValue = m_BlockWidthAction.ReadValue<float>();

                if (scrollValue > 0) {
                    IncreaseBlockWidth();
                } else if (scrollValue < 0) {
                    DecreaseBlockWidth();
                }
            }

            if (m_BlockDepthAction.IsInProgress()) {
                var scrollValue = m_BlockDepthAction.ReadValue<float>();

                if (scrollValue > 0) {
                    IncreaseBlockDepth();
                } else if (scrollValue < 0) {
                    DecreaseBlockDepth();
                }
            }

            if (m_SetbackAction.IsInProgress()) {
                var scrollValue = m_SetbackAction.ReadValue<float>();
                var currentSnap = m_SnapSystem.CurrentSnapSetback;

                if (scrollValue > 0) {
                    SetSnapSpacing(currentSnap + 1f);
                } else if (scrollValue < 0) {
                    SetSnapSpacing(currentSnap - 1f);
                }
            }

            if (m_BlockSizeAction.IsInProgress()) {
                var scrollValue = m_BlockSizeAction.ReadValue<float>();

                if (scrollValue > 0) {
                    IncreaseBlockDepth();
                    IncreaseBlockWidth();
                } else if (scrollValue < 0) {
                    DecreaseBlockDepth();
                    DecreaseBlockWidth();
                }
            }
        }

        /// <summary>
        /// Listen to mouswheel input for adjusting block size.
        /// </summary>
        private void HandleMousewheelInput() { }

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
        private bool ShouldRenderOverlay() { return m_ToolSystem.activeTool is not DefaultToolSystem || m_ToolSystem.activePrefab is ParcelPlaceholderPrefab; }

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
        /// Called from the shortcut to open the Platter panel with default parcel.
        /// </summary>
        private void OpenPlatterPanel() {
            m_Log.Debug("OpenPlatterPanel()");
            UpdateSelectedPrefab();
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
    }
}