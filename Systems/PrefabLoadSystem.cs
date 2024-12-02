namespace Platter.Systems {
    using System.Collections.Generic;
    using System.Linq;
    using cohtml.Net;
    using Colossal.IO.AssetDatabase;
    using Colossal.IO.AssetDatabase.Internal;
    using Colossal.Json;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Colossal.UI.Binding;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.UI;
    using HarmonyLib;
    using Platter;
    using Platter.Prefabs;
    using Platter.Utils;
    using Unity.Entities;
    using UnityEngine;

    public partial class PrefabLoadSystem : UISystemBase {
        private PrefixedLogger m_Log;
        private RandomSeed m_Random;
        private EntityArchetype m_DefinitionArchetype;
        private EndFrameBarrier m_EndFrameBarrier;
        private EntityQuery m_BuildingQuery;
        private bool executed = false;
        private EndFrameBarrier m_Barrier;
        private EntityCommandBuffer m_CommandBuffer;
        private EntityCommandBuffer m_BlockCommandBuffer;
        private Entity m_CachedBuildingEntity;
        private Entity m_CachedEdgeEntity;
        private EntityQuery m_UpdatedEdgesQuery;
        public static readonly string finalPrefabName = "ZONETEST"; // temp until we iterate
        private static bool installed;
        private static World world;
        private static PrefabSystem prefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(nameof(PrefabLoadSystem));
            m_Log.Debug($"Loaded System.");
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {

        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);

            if (installed) {
                m_Log.Debug($"Prefab is installed, skipping");
                return;
            }

            // Getting World instance
            world = Traverse.Create(GameManager.instance).Field<World>("m_World").Value;
            if (world == null) {
                m_Log.Error($"Failed retrieving World instance, exiting.");
                return;
            }

            m_Log.Debug($"Retrieved World instance.");

            // Getting PrefabSystem instance from World
            prefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();

            // Retrieve all prefabs needed
            bool error = false;
            if (!prefabSystem.TryGetPrefab(new PrefabID("ZonePrefab", "EU Residential Mixed"), out PrefabBase zonePrefab)) {
                m_Log.Error($"Failed retrieving original Prefabs and Components, exiting. zonePrefab not found");
                error = true;
            }
            if (!prefabSystem.TryGetPrefab(new PrefabID("BuildingPrefab", "ParkingLot01"), out PrefabBase parkingLotPrefab)) {
                m_Log.Error($"Failed retrieving original Prefabs and Components, exiting. parkingLotPrefab not found");
                error = true;
            }
            if (!prefabSystem.TryGetPrefab(new PrefabID("RoadPrefab", "Alley"), out PrefabBase roadPrefabBase)) {
                m_Log.Error($"Failed retrieving original Prefabs and Components, exiting. roadPrefabBase not found");
                error = true;
            }
            if (!zonePrefab.TryGetExactly<UIObject>(out var zonePrefabUIObject)) {
                m_Log.Error($"Failed retrieving original Prefabs and Components, exiting. zonePrefabUIObject not found");
                error = true;
            }
            if (!parkingLotPrefab.TryGetExactly<ObjectSubLanes>(out var parkingFences)) {
                m_Log.Error($"Failed retrieving original Prefabs and Components, exiting. parkingFences not found");
                error = true;
            }

            if (error) {
                error = true;
            }

            // Cast the road prefab
            RoadPrefab roadPrefab = (RoadPrefab)roadPrefabBase;

            m_Log.Debug($"Retrieved the original Area Prefab instance.");

            // Try instead making the new one manually.
            var placeableLotPrefab = ScriptableObject.CreateInstance<ParcelPrefab>();
            placeableLotPrefab.name = finalPrefabName;
            // Adding PlaceableObject Data.
            var placeableObject = ScriptableObject.CreateInstance<PlaceableObject>();
            placeableObject.m_ConstructionCost = 0;
            placeableObject.m_XPReward = 0;
            placeableLotPrefab.AddComponentFrom<PlaceableObject>(placeableObject);
            // Adding ZoneBlock data. 
            placeableLotPrefab.m_ZoneBlock = roadPrefab.m_ZoneBlock;
            // Create and populate the new UIObject for our cloned Prefab
            var placeableLotPrefabUIObject = ScriptableObject.CreateInstance<UIObject>();
            placeableLotPrefabUIObject.m_Icon = zonePrefabUIObject.m_Icon;
            placeableLotPrefabUIObject.name = finalPrefabName;
            placeableLotPrefabUIObject.m_IsDebugObject = zonePrefabUIObject.m_IsDebugObject;
            placeableLotPrefabUIObject.m_Priority = zonePrefabUIObject.m_Priority;
            placeableLotPrefabUIObject.m_Group = zonePrefabUIObject.m_Group;
            placeableLotPrefabUIObject.active = zonePrefabUIObject.active;
            placeableLotPrefab.AddComponentFrom(placeableLotPrefabUIObject);

            // Create subarea
            //var placeableLotSubLanes = ScriptableObject.CreateInstance<ObjectSubLanes>();
            //placeableLotSubLanes.m_SubLanes = parkingFences.m_SubLanes;
            //placeableLotPrefab.AddComponentFrom(placeableLotSubLanes);

            if (!prefabSystem.AddPrefab(placeableLotPrefab)) {
                m_Log.Error($"Failed adding the placeableLotPrefab to PrefabSystem, exiting.");
                return;
            }

            m_Log.Debug($"Successfully created and added our placeableLotPrefab to PrefabSystem.");

            // Mark the Install as already executed
            installed = true;

            m_Log.Debug($"Completed.");
        }

    }
}
