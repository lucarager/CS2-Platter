// <copyright file="PlatterToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Areas;
    using Game.City;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Tools;
    using Platter.Settings;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    public partial class PlatterToolSystem : ObjectToolBaseSystem {
        /// <summary>
        /// Todo.
        /// </summary>
        public void RequestEnable() {
            m_Log.Debug($"RequestEnable()");

            if (m_ToolSystem.activeTool != this) {
                m_PreviousTool = m_ToolSystem.activeTool;

                // Check for valid prefab selection before continuing.
                // ObjectToolSystem objectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
                // SelectedPrefab = objectToolSystem.prefab;
                m_ToolSystem.selected = Entity.Null;
                m_ToolSystem.activeTool = this;
                UpdateSelectedPrefab();
            }
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void RequestDisable() {
            m_Log.Debug($"RequestDisable()");

            m_ToolSystem.activeTool = m_DefaultToolSystem;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void RequestToggle() {
            m_Log.Debug($"RequestToggle()");

            if (m_ToolSystem.activeTool == this) {
                RequestDisable();
            } else {
                RequestEnable();
            }
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            // References & State
            Instance = this;
            Enabled = false;

            base.OnCreate();

            // Logging
            m_Log = new PrefixedLogger(nameof(PlatterToolSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_HighlightedQuery = GetEntityQuery(ComponentType.ReadOnly<Highlighted>());
            m_DefinitionQuery = GetDefinitionQuery();

            // Get Systems
            m_RaycastSystem = World.GetOrCreateSystemManaged<Game.Common.RaycastSystem>();
            m_CameraUpdateSystem = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            m_OverlayBuffer = World.GetOrCreateSystemManaged<OverlayRenderSystem>().GetBuffer(out var _);
            m_CityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            m_PlatterOverlaySystem = World.GetOrCreateSystemManaged<PlatterOverlaySystem>();
            m_ObjectSearchSystem = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_NetSearchSystem = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_ZoneSearchSystem = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();

            // Buffers
            m_ControlPoints = new NativeList<ControlPoint>(1, Allocator.Persistent);
            m_Points = new();

            // Position this tool before vanilla tools. Code copied from LineTool.
            // todo is this necessary?
            var toolList = World.GetOrCreateSystemManaged<ToolSystem>().tools;
            ToolBaseSystem thisSystem = null;
            var objectToolIndex = 0;

            for (var i = 0; i < toolList.Count; i++) {
                var tool = toolList[i];
                m_Log.Debug($"Got tool {tool.toolID} ({tool.GetType().FullName})");
                if (tool == this) {
                    m_Log.Debug("Found Platter Tool reference in tool list");
                    thisSystem = tool;
                }

                if (tool.toolID.Equals("Object Tool")) {
                    objectToolIndex = i;
                }
            }

            // Remove existing tool reference.
            if (thisSystem is not null) {
                toolList.Remove(this);
            }

            toolList.Insert(objectToolIndex - 1, this);

            // Set apply and cancel actions from game settings.
            m_ApplyAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.ApplyActionName);
            m_CreateAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.CreateActionName);
            m_CancelAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.CancelActionName);

            //// Enable fixed preview control.
            // _fixedPreviewAction = new("LineTool-FixPreview");
            // _fixedPreviewAction.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/ctrl").With("Button", "<Mouse>/leftButton");
            // _fixedPreviewAction.Enable();

            //// Enable keep building action.
            // _keepBuildingAction = new("LineTool-KeepBuilding");
            // _keepBuildingAction.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/shift").With("Button", "<Mouse>/leftButton");
            // _keepBuildingAction.Enable();
        }

        /// <inheritdoc/>
        protected override void OnStartRunning() {
            m_Log.Debug($"OnStartRunning()");

            base.OnStartRunning();

            // Ensure apply action is enabled.
            m_ApplyAction.shouldBeEnabled = true;
            m_CancelAction.shouldBeEnabled = true;

            // Clear any previous raycast result.
            m_LastHitPosition = default;

            // Reset any previously-stored starting position.
            // m_ModeData.Reset();

            // Clear any applications.
            applyMode = ApplyMode.Clear;

            // Manually enable base action if in editor mode.
            if (m_ToolSystem.actionMode.IsEditor()) {
                applyAction.enabled = true;
            }

            // Visualization
            base.requireZones = true;
            base.requireAreas = AreaTypeMask.Lots;
        }

        /// <inheritdoc/>
        protected override void OnStopRunning() {
            base.OnStopRunning();
            m_Log.Debug($"OnStopRunning()");

            // Cleanup
            Reset();
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_Log.Debug($"OnDestroy()");

            base.OnDestroy();
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

            var result = TrySetPrefab(prefabBase);

            m_Log.Debug($"UpdateSelectedPrefab() -- Result {result}");

            // m_Prefab = prefabBase;
            // m_PrefabDataBinding.Update(new PrefabUIData(prefabBase.name, ImageSystem.GetThumbnail(prefabBase)));
            // TryActivatePrefabTool(m_Prefab);
        }

        public NativeList<ControlPoint> GetControlPoints(out JobHandle dependencies) {
            dependencies = base.Dependency;
            return this.m_ControlPoints;
        }

        private ObjectPrefab GetObjectPrefab() {
            return this.m_SelectedPrefab;
        }

        /// <summary>
        /// Restores the previously-used tool.
        /// </summary>
        internal void RestorePreviousTool() {
            if (m_PreviousTool is not null) {
                m_ToolSystem.activeTool = m_PreviousTool;
                CurrentMode = PlatterToolMode.Plop;
            } else {
                m_Log.Error("null tool set when restoring previous tool");
            }
        }
    }
}
