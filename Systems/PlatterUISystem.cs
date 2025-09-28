// <copyright file="PlatterUISystem.cs" company="Luca Rager">
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
    public partial class PlatterUISystem : ExtendedUISystemBase {
        public ZoneType PreZoneType { get; set; } = ZoneType.None;

        public bool AllowSpawning { get; set; } = true;

        // Systems
        private PrefabSystem m_PrefabSystem;
        private ToolSystem m_ToolSystem;
        private ZoneSystem m_ZoneSystem;
        private ObjectToolSystem m_ObjectToolSystem;
        private ImageSystem m_ImageSystem;
        private CityConfigurationSystem m_CityConfigurationSystem;
        private PlatterOverlaySystem m_PlatterOverlaySystem;
        private ParcelSpawnSystem m_ParcelSpawnSystem;

        // Queries
        private EntityQuery m_ZoneQuery;
        private EntityQuery m_UIAssetCategoryDataQuery;
        private EntityQuery m_Query;

        // Logger
        private PrefixedLogger m_Log;

        // Data
        private int2 m_SelectedParcelSize = new(2, 2);
        private List<ZoneUIData> m_ZoneData;
        private Dictionary<int, ZoneType> m_ZoneTypeData;
        private bool loadZones = true;
        private static readonly Dictionary<string, Entity> MAssetMenuDataDict = new();
        private List<Entity> m_SelectedThemes;
        private List<Entity> m_SelectedAssetPacks;

        // Bindings
        private ValueBindingHelper<bool> m_EnableToolButtonsBinding;
        private ValueBindingHelper<int> m_ZoneBinding;
        private ValueBindingHelper<PrefabUIData> m_PrefabDataBinding;
        private ValueBindingHelper<ZoneUIData[]> m_ZoneDataBinding;
        private ValueBindingHelper<int> m_BlockWidthBinding;
        private ValueBindingHelper<int> m_BlockDepthBinding;
        private ValueBindingHelper<bool> m_RenderParcelsBinding;
        private ValueBindingHelper<bool> m_AllowSpawningBinding;

        // Shortcuts
        private ProxyAction m_IncreaseBlockWidthAction;
        private ProxyAction m_IncreaseBlockDepthAction;
        private ProxyAction m_DecreaseBlockWidthAction;
        private ProxyAction m_DecreaseBlockDepthAction;
        private IProxyAction m_PreciseRotationAction;
        private ProxyAction m_ToggleRender;
        private ProxyAction m_ToggleSpawn;

        /// <summary>
        /// Todo.
        /// </summary>
        public readonly struct PrefabUIData : IJsonWritable {
            private readonly string Name;
            private readonly string Thumbnail;

            /// <summary>
            /// Initializes a new instance of the <see cref="PrefabUIData"/> struct.
            /// </summary>
            public PrefabUIData(string name, string thumbnail) {
                Name = name;
                Thumbnail = thumbnail;
            }

            /// <inheritdoc/>
            public readonly void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("name");
                writer.Write(Name);

                writer.PropertyName("thumbnail");
                writer.Write(Thumbnail);

                writer.TypeEnd();
            }
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public readonly struct ZoneUIData : IJsonWritable {
            private readonly string Name;
            private readonly string Thumbnail;
            private readonly string Group;
            private readonly ushort Index;

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

            m_Query = GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<UIAssetMenuData>(),
            });

            m_UIAssetCategoryDataQuery = GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<ZoneData>(),
                ComponentType.ReadOnly<UIObjectData>(),
            });

            // todo
            // use
            // m_ToolSystem.EventPrefabChanged += OnPrefabChanged;

            // Systems
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ZoneSystem = World.GetOrCreateSystemManaged<ZoneSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_ImageSystem = World.GetOrCreateSystemManaged<ImageSystem>();
            m_CityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            m_PlatterOverlaySystem = World.GetOrCreateSystemManaged<PlatterOverlaySystem>();
            m_ParcelSpawnSystem = World.GetOrCreateSystemManaged<ParcelSpawnSystem>();

            // Data
            m_SelectedThemes = new List<Entity>();
            m_SelectedAssetPacks = new List<Entity>();

            // Bindings
            m_EnableToolButtonsBinding = CreateBinding<bool>("ENABLE_TOOL_BUTTONS", false);
            m_ZoneBinding = CreateBinding<int>("ZONE", 0, SetPreZone);
            m_BlockWidthBinding = CreateBinding<int>("BLOCK_WIDTH", 2);
            m_BlockDepthBinding = CreateBinding<int>("BLOCK_DEPTH", 2);
            PrefabUIData defaultParcel = new("Parcel 2x2", "coui://platter/Parcel_2x2.svg");
            m_PrefabDataBinding = CreateBinding<PrefabUIData>("PREFAB_DATA", defaultParcel);
            m_ZoneDataBinding = CreateBinding<ZoneUIData[]>("ZONE_DATA", m_ZoneData.ToArray());
            m_RenderParcelsBinding = CreateBinding<bool>("RENDER_PARCELS", true, SetRenderParcels);
            m_AllowSpawningBinding = CreateBinding<bool>("ALLOW_SPAWNING", true, SetAllowSpawning);

            // Triggers
            CreateTrigger<string>("ADJUST_BLOCK_SIZE", HandleBlockSizeAdjustment);

            // Shortcuts
            m_IncreaseBlockWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelWidthActionName);
            m_IncreaseBlockDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.IncreaseParcelDepthActionName);
            m_DecreaseBlockWidthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelWidthActionName);
            m_DecreaseBlockDepthAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.DecreaseParcelDepthActionName);
            m_PreciseRotationAction = (IProxyAction)m_ObjectToolSystem.GetMemberValue("m_PreciseRotation");
            m_ToggleRender = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.ToggleRenderActionName);
            m_ToggleSpawn = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.ToggleSpawnActionName);

            m_ToolSystem.EventToolChanged += OnToolChanged;
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

            if (m_PreciseRotationAction.IsInProgress()) {
                var num = m_PreciseRotationAction.ReadValue<float>();
                m_Log.Debug($"{num}");
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
                m_ZoneDataBinding.Value = m_ZoneData.ToArray();
            }

            // Update selected Prefab binding
            var prefabBase = m_ObjectToolSystem.prefab;
            if (prefabBase != null) {
                m_PrefabDataBinding.Value = new PrefabUIData(prefabBase.name, ImageSystem.GetThumbnail(prefabBase));
            }
        }

        private void OnToolChanged(ToolBaseSystem tool) {
        }

        /// <inheritdoc/>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);
            m_Log.Debug($"OnGameLoadingComplete(purpose={purpose}, mode={mode})");

            if (purpose == Purpose.LoadGame && mode == GameMode.Game) {
                CollectData();
            }

            m_ToggleRender.shouldBeEnabled = mode.IsGameOrEditor();
            m_ToggleSpawn.shouldBeEnabled = mode.IsGameOrEditor();
        }

        private readonly string[] m_ZoneUITags = {
            "ZonesResidential",
            "ZonesCommercial",
            "ZonesIndustrial",
            "ZonesOffice",
            "ByLawZones",
        };

        private void CollectData() {
            m_Log.Debug($"CollectData");

            var entities = m_Query.ToEntityArray(Allocator.Temp);

            m_ZoneData.Clear();
            m_ZoneTypeData.Clear();

            if (m_PrefabSystem.TryGetPrefab(new PrefabID("ZonePrefab", "Unzoned"), out var unzonedPrefabBase) &&
                m_PrefabSystem.TryGetEntity(unzonedPrefabBase, out var unzonedEntity)) {
                var zoneData = EntityManager.GetComponentData<ZoneData>(unzonedEntity);

                m_ZoneData.Add(new ZoneUIData(
                    unzonedPrefabBase.name,
                    ImageSystem.GetThumbnail(unzonedPrefabBase),
                    "Unzoned",
                    0
                ));
                m_ZoneTypeData.Add(zoneData.m_ZoneType.m_Index, zoneData.m_ZoneType);
            } else {
                m_Log.Error($"Failed retrieving unzonedPrefabBase");
            }

            foreach (var assetMenuEntity in entities) {
                EntityManager.TryGetBuffer<UIGroupElement>(assetMenuEntity, true, out var categoryBuffer);
                var sortedCategories = GetCategories(categoryBuffer);

                foreach (var category in sortedCategories) {
                    var categoryEntity = category.entity;
                    var categoryPrefab = m_PrefabSystem.GetPrefab<PrefabBase>(category.prefabData);

                    m_Log.Debug($"CollectData -- categoryPrefab.name {categoryPrefab.name}");

                    if (EntityManager.TryGetBuffer<UIGroupElement>(categoryEntity, true, out var assetsBuffer)) {
                        var assets = UIObjectInfo.GetObjects(EntityManager, assetsBuffer, Allocator.TempJob);
                        FilterOutUpgrades(assets);
                        assets.Sort<UIObjectInfo>();

                        foreach (var asset in assets) {
                            var assetEntity = asset.entity;
                            if (!m_PrefabSystem.TryGetPrefab<ZonePrefab>(assetEntity, out var zonePrefab)) {
                                return;
                            }

                            var zoneData = EntityManager.GetComponentData<ZoneData>(assetEntity);

                            m_ZoneTypeData.Add(zoneData.m_ZoneType.m_Index, zoneData.m_ZoneType);
                            m_ZoneData.Add(new ZoneUIData(
                                zonePrefab.name,
                                ImageSystem.GetThumbnail(zonePrefab),
                                categoryPrefab.name,
                                zoneData.m_ZoneType.m_Index
                            ));

                            m_Log.Debug($"CollectData -- categoryEntity {categoryPrefab.name} -- asset.name {zonePrefab.name}");
                        }
                    }
                }
            }
        }

        private NativeList<UIObjectInfo> GetCategories(DynamicBuffer<UIGroupElement> elements) {
            var objects = UIObjectInfo.GetObjects(EntityManager, elements, Allocator.TempJob);
            for (var i = objects.Length - 1; i >= 0; i--) {
                if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(objects[i].prefabData, out var categoryPrefab) ||
                    !m_ZoneUITags.Contains(categoryPrefab.name) ||
                    !EntityManager.HasComponent<UIAssetCategoryData>(objects[i].entity) ||
                    !EntityManager.TryGetBuffer<UIGroupElement>(objects[i].entity, true, out var dynamicBuffer) ||
                    dynamicBuffer.Length == 0) {
                    objects.RemoveAtSwapBack(i);
                }
            }

            objects.Sort<UIObjectInfo>();
            return objects;
        }

        private void FilterOutUpgrades(NativeList<UIObjectInfo> elementInfos) {
            for (int i = elementInfos.Length - 1; i >= 0; i--) {
                if (base.EntityManager.HasComponent<ServiceUpgradeData>(elementInfos[i].entity)) {
                    elementInfos.RemoveAtSwapBack(i);
                }
            }
        }

        /// <summary>
        /// </summary>
        private bool CurrentlyUsingParcelsInObjectTool() {
            return m_ToolSystem.activeTool is ObjectToolSystem objectToolSystem && m_ObjectToolSystem.prefab is ParcelPrefab;
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
