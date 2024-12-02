namespace Platter.Systems {
    using System.Collections.Generic;
    using System.Linq;
    using Game.Prefabs;
    using Game.SceneFlow;
    using HarmonyLib;
    using Platter;
    using Platter.Prefabs;
    using Unity.Entities;
    using UnityEngine;

    internal static class PrefabManager {
        private static readonly string prefabToCloneName = "Clear Area";
        public static readonly string prefabName = "ZONETEST";
        private static bool installed;
        private static World world;
        private static PrefabSystem prefabSystem;

        internal static void Install() {
            var logHeader = nameof(PrefabManager);

            if (installed) {
                Mod.Instance.Log.Debug($"{logHeader} Prefab is installed, skipping");
                return;
            }

            // Getting World instance
            world = Traverse.Create(GameManager.instance).Field<World>("m_World").Value;
            if (world == null) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving World instance, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Retrieved World instance.");

            // Getting PrefabSystem instance from World
            prefabSystem = world.GetExistingSystemManaged<PrefabSystem>();
            if (prefabSystem == null) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving PrefabSystem instance, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Retrieved PrefabSystem instance.");

            // Getting Prefabs list from PrefabSystem
            var prefabs = Traverse.Create(prefabSystem).Field<List<PrefabBase>>("m_Prefabs").Value;
            if (prefabs == null || !prefabs.Any()) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving Prefabs list, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Retrieved Prefabs list.");
            Mod.Instance.Log.Debug($"{logHeader} {prefabs}");

            // Getting the original Area Prefab
            var areaPrefab = (SpacePrefab) prefabs.FirstOrDefault(p => p.name == prefabToCloneName);
            if (areaPrefab == null) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving the original Area Prefab instance, exiting.");
                return;
            }
            var roadPrefab = (RoadPrefab) prefabs.FirstOrDefault(p => p.name == "Alley");
            if (roadPrefab == null) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving the original Road Prefab instance, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Retrieved the original Area Prefab instance.");

            // Instantiate our clone copying all the properties over
            var parcelPrefab = areaPrefab.Clone(prefabName);

            Mod.Instance.Log.Debug($"{logHeader} Cloned the original Area Prefab instance.");

            // Modify this prefab so that it has some additional things. 
            var parcel = ScriptableObject.CreateInstance<_areaBasedParcel>();
            parcel.m_ZoneBlock = roadPrefab.m_ZoneBlock;
            var parcelComponent = parcelPrefab.AddComponentFrom(parcel);

            if (!prefabSystem.AddPrefab(parcelPrefab)) {
                Mod.Instance.Log.Error($"{logHeader} Failed adding the cloned Prefab to PrefabSystem, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Successfully created and added our cloned Prefab to PrefabSystem.");

            // Mark the Install as already executed
            installed = true;

            Mod.Instance.Log.Debug($"{logHeader} Completed.");
        }
    }
}
