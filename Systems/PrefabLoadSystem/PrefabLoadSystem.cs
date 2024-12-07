namespace Platter.Systems {
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.UI;
    using HarmonyLib;
    using Platter.Utils;
    using Unity.Entities;

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
        private static bool installed;
        private static bool postInstalled;
        private static World world;
        private static PrefabSystem prefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(PrefabLoadSystem));
            m_Log.Debug($"OnCreate()");
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            var logMethodPrefix = $"OnGamePreload(purpose {purpose}, mode {mode}) --";

            if (installed) {
                m_Log.Debug($"{logMethodPrefix} Prefab is installed, skipping");
                return;
            }

            // Getting World instance
            world = Traverse.Create(GameManager.instance).Field<World>("m_World").Value;
            if (world == null) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving World instance, exiting.");
                return;
            }

            m_Log.Debug($"{logMethodPrefix} Retrieved World instance.");

            // Getting PrefabSystem instance from World
            prefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            if (!prefabSystem.TryGetPrefab(new PrefabID("ZonePrefab", "EU Residential Mixed"), out PrefabBase zonePrefab)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. zonePrefab not found");
            }
            if (!prefabSystem.TryGetPrefab(new PrefabID("BuildingPrefab", "ParkingLot01"), out PrefabBase parkingLotPrefab)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. parkingLotPrefab not found");
            }
            if (!prefabSystem.TryGetPrefab(new PrefabID("RoadPrefab", "Alley"), out PrefabBase roadPrefabBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. roadPrefabBase not found");
            }
            if (!prefabSystem.TryGetPrefab(new PrefabID("NetLaneGeometryPrefab", "EU Car Bay Line"), out var netLanePrefabBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. NetLaneGeometryPrefab not found");
            }
            if (!prefabSystem.TryGetPrefab(new PrefabID("StaticObjectPrefab", "NA RoadArrow Forward"), out var roadArrowFwdbBase)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. NetLaneGeometryPrefab not found");
            }
            if (!zonePrefab.TryGetExactly<UIObject>(out var zonePrefabUIObject)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. zonePrefabUIObject not found");
            }
            if (!parkingLotPrefab.TryGetExactly<ObjectSubLanes>(out var parkingFences)) {
                m_Log.Error($"{logMethodPrefix} Failed retrieving original Prefabs and Components, exiting. parkingFences not found");
            }

            m_Log.Debug($"{logMethodPrefix} Successfully found all required prefabs and components.");

            // Cast prefabs
            var roadPrefab = (RoadPrefab)roadPrefabBase;
            var netLaneGeoPrefab = (NetLaneGeometryPrefab)netLanePrefabBase;
            var roadArrowFwd = (StaticObjectPrefab)roadArrowFwdbBase;

            for (int i = blockSizes.x; i <= blockSizes.z; i++) {
                for (int j = blockSizes.y; j <= blockSizes.w; j++) {
                    if (!CreatePrefab(i, j, roadPrefab, netLaneGeoPrefab, zonePrefabUIObject, roadArrowFwd)) {
                        m_Log.Error($"{logMethodPrefix} Failed adding ParcelPrefab {i}x{j} to PrefabSystem, exiting prematurely.");
                        return;
                    }
                    m_Log.Debug($"{logMethodPrefix} Successfully created and added ParcelPrefab {i}x{j} to PrefabSystem.");
                }
            }

            // Mark the Install as already executed
            installed = true;

            m_Log.Debug($"{logMethodPrefix} Completed.");
        }
    }
}
