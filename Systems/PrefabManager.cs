// <copyright file="UpgradesManager.cs" company="Yenyang's Mods. MIT License">
// Copyright (c) Yenyang's Mods. MIT License. All rights reserved.
// </copyright>

// This code was originally part of Extended Road Upgrades by ST-Apps. It has been incorporated into this project with permission of ST-Apps.
namespace Platter.Systems {
    using System.Collections.Generic;
    using System.Linq;
    using Colossal.Json;
    using Game.Prefabs;
    using Game.SceneFlow;
    using HarmonyLib;
    using Platter;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// This class provides the utility methods to install the mod.
    /// </summary>
    internal static class PrefabManager {
        /// <summary>
        ///     Base URI for all of our icons.
        /// </summary>
        private static readonly string COUIBaseLocation = $"coui://uil/Standard/RoadUpgrade";

        /// <summary>
        ///     Guard boolean used to check if the Prefix already executed, so that we can prevent executing it multiple times.
        /// </summary>
        private static bool installed;

        /// <summary>
        ///     Guard boolean used to check if the Event Handler already executed, so that we can prevent executing it multiple times.
        /// </summary>
        private static bool postInstalled;

        /// <summary>
        ///     <see cref="world"/> instance used by our patch and by the loading event handler.
        /// </summary>
        private static World world;

        /// <summary>
        ///     <see cref="prefabSystem"/> instance used by our patch and by the loading event handler. 
        /// </summary>
        private static PrefabSystem prefabSystem;

        /// <summary>
        ///     <para>
        ///         Installing the mode means to add our cloned <see cref="PrefabBase"/> to the global collection in
        ///         <see cref="prefabSystem"/>.
        ///     </para>
        ///     <para>
        ///         To avoid getting the wrong <see cref="world"/> instance we rely on Harmony's <see cref="Traverse"/> to extract the
        ///         <b>m_World</b> field from the injected <see cref="GameManager"/> instance.
        ///     </para>
        ///     <para>
        ///         After that, we leverage <see cref="World.GetOrCreateSystemManaged{T}"/> to get our target <see cref="prefabSystem"/>.
        ///         From there, to get <see cref="prefabSystem"/>'s internal <see cref="PrefabBase"/> list we use <see cref="Traverse"/>
        ///         again and we extract the <b>m_Prefabs</b> field.
        ///     </para>
        ///     <para>
        ///         We now have what it takes to extract our <see cref="PrefabBase"/> object, and as reference we extract the one called
        ///         <b>Landfill</b>.
        ///         During this stage we only care for <see cref="ComponentBase"/> and not <see cref="IComponentData"/>.
        ///     </para>
        ///     <para>
        ///         The only <see cref="ComponentBase"/> we need to deal with is the attached <see cref="UIObject"/>, which contains the
        ///         <see cref="UIObject.m_Icon"/> property. This property is a relative URI pointing to a SVG file in your
        ///         <b>Cities2_Data\StreamingAssets\~UI~\GameUI\Media\Game\Icons</b> directory.
        ///     </para>
        ///     <para>
        ///         The <b>Cities2_Data\StreamingAssets\~UI~\GameUI\</b> MUST be omitted from the URI, resulting in a definition similar to:
        ///         <code>
        ///             myUIObject.m_Icon = "Media\Game\Icons\myIcon.svg
        ///         </code>
        ///     </para>
        ///     <para>
        ///         Once the <see cref="UIObject"/> is properly set with an updated <see cref="UIObject.m_Icon"/> and <see cref="UIObject.name"/>
        ///     </para>
        /// </summary>
        internal static void Install() {
            var logHeader = $"[{nameof(PrefabManager)}.{nameof(Install)}]";

            if (installed) {
                Mod.Instance.Log.Info($"{logHeader} Extended Upgrades is installed, skipping");
                return;
            }

            Mod.Instance.Log.Info($"{logHeader} Installing Extended Upgrades");

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

            // Getting the original Landfill Prefab
            var lotPrefab = prefabs.FirstOrDefault(p => p.name == "Landfill Site Lot");
            if (lotPrefab == null) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving the original Landfill Prefab instance, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Retrieved the original Landfill Prefab instance.");

            // Getting the original Landfill Prefab's UIObject
            var lotUIObject = lotPrefab.GetComponent<UIObject>();
            if (lotUIObject == null) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving the original Landfill Prefab's UIObject instance, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Retrieved the original Landfill Prefab's UIObject instance.");

            // We now have all the needed original objects to build our clones
            //foreach (var upgradeMode in ExtendedRoadUpgrades.Modes) {
            //    if (prefabSystem.TryGetPrefab(new PrefabID("FencePrefab", upgradeMode.ObsoleteId), out PrefabBase prefabBase)) {
            //        Mod.Instance.Log.Debug($"{logHeader} [{upgradeMode.ObsoleteId}] Already exists.");
            //        return;
            //    }

            // Instantiate our clone copying all the properties over
            var clonedLotPrefab = Object.Instantiate(lotPrefab);

            // Replace the name our they will be called "Landfill (Clone)"
            clonedLotPrefab.name = "ZONETEST";

            Mod.Instance.Log.Debug($"{logHeader} Cloned the original Landfill Prefab instance.");

            //    // Update the UI component.
            //    // To avoid impacting the Landfill prefab we need to replace the UIObject with
            //    // a fresh instance. Every property besides name and icon can be copied over.
            //    // There is probably a better way of doing this, but I need to be sure that we're not
            //    // keeping any unintended reference to the source object so I'd rather manually copy
            //    // over only the thing I need instead of relying on automatic cloning.
            //    clonedLandfillUpgradePrefab.Remove<UIObject>();

            //    Mod.Instance.Log.Debug($"{logHeader} [{upgradeMode.Id}] Removed the original UIObject instance from the cloned Prefab.");

            //    // Create and populate the new UIObject for our cloned Prefab
            //    var clonedLandfillUpgradePrefabUIObject = ScriptableObject.CreateInstance<UIObject>();
            //    clonedLandfillUpgradePrefabUIObject.m_Icon = $"{COUIBaseLocation}{upgradeMode.ObsoleteId}.svg";
            //    clonedLandfillUpgradePrefabUIObject.name = LandfillUpgradePrefabUIObject.name.Replace("Landfill", upgradeMode.Id);
            //    clonedLandfillUpgradePrefabUIObject.m_IsDebugObject = LandfillUpgradePrefabUIObject.m_IsDebugObject;
            //    clonedLandfillUpgradePrefabUIObject.m_Priority = LandfillUpgradePrefabUIObject.m_Priority;
            //    clonedLandfillUpgradePrefabUIObject.m_Group = LandfillUpgradePrefabUIObject.m_Group;
            //    clonedLandfillUpgradePrefabUIObject.active = LandfillUpgradePrefabUIObject.active;

            //    Mod.Instance.Log.Debug($"{logHeader} [{upgradeMode.Id}] Created a custom UIObject for our cloned Prefab with name {clonedLandfillUpgradePrefabUIObject.name} and icon {clonedLandfillUpgradePrefabUIObject.m_Icon}.");

            // Add the newly created UIObject component and then add the cloned Prefab to our PrefabSystem
            //clonedLotPrefab.AddComponentFrom(clonedLandfillUpgradePrefabUIObject);
            if (!prefabSystem.AddPrefab(clonedLotPrefab)) {
                Mod.Instance.Log.Error($"{logHeader} Failed adding the cloned Prefab to PrefabSystem, exiting.");
                return;
            }

            Mod.Instance.Log.Info($"{logHeader} Successfully created and added our cloned Prefab to PrefabSystem.");

            // Attach to GameManager's loading event to perform the second phase of our patch
            GameManager.instance.onGameLoadingComplete += GameManager_onGameLoadingComplete;
            Mod.Instance.Log.Info($"{logHeader} Ready to listen to GameManager loading events.");

            // Mark the Install as already executed
            installed = true;

            Mod.Instance.Log.Info($"{logHeader} Completed.");
            //}
        }

        /// <summary>
        ///     <para>
        ///         This event handler performs the second phase of our custom modes patching.
        ///     </para>
        ///     <para>
        ///         While in the first phase we create the <see cref="PrefabBase"/> without any <see cref="IComponentData"/>, in this one
        ///         we add the <see cref="IComponentData"/> that we need to define the behavior of our custom upgrade modes.
        ///     </para>
        ///     <para>
        ///         This behavior is defined by the <see cref="PlaceableNetData"/> <see cref="IComponentData"/>, which allows us to specify
        ///         a collection of <see cref="PlaceableNetData.m_SetUpgradeFlags"/> and <see cref="PlaceableNetData.m_UnsetUpgradeFlags"/>.
        ///     </para>
        ///     <para>
        ///         These two collection contains all the needed <see cref="CompositionFlags"/> that game will use to compose the final road.
        ///     </para>
        ///     <para>
        ///         <list type="bullet">
        ///             <ul>
        ///                 <see cref="PlaceableNetData.m_SetUpgradeFlags"/> contains the flags that must be added to the target road piece
        ///             </ul>
        ///             <ul>
        ///                 <see cref="PlaceableNetData.m_UnsetUpgradeFlags"/> contains the flags that must be removed from the target road piece
        ///             </ul>
        ///         </list>
        ///     </para>
        ///     <para>
        ///         The goal of this method is then to simply iterate over our cloned <see cref="PrefabBase"/> Prefabs and add to each one of them
        ///         the appropriate <see cref="PlaceableNetData"/>, based on the data set in our <see cref="Data.ExtendedRoadUpgrades.Modes"/> collection.
        ///     </para>
        /// </summary>
        /// <param name="purpose"></param>
        /// <param name="mode"></param>
        private static void GameManager_onGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, Game.GameMode mode) {
            var logHeader = $"[{nameof(PrefabManager)}.{nameof(GameManager_onGameLoadingComplete)}]";

            if (postInstalled) {
                Mod.Instance.Log.Info($"{logHeader} Already executed before, skipping.");
                return;
            }

            // Execute in Game mode only
            if (mode != Game.GameMode.Game && mode != Game.GameMode.Editor) {
                Mod.Instance.Log.Info($"{logHeader} Game mode is {mode}, skipping.");
                return;
            }

            Mod.Instance.Log.Info($"{logHeader} Started.");

            // Getting Prefabs list from PrefabSystem
            var prefabs = Traverse.Create(prefabSystem).Field<List<PrefabBase>>("m_Prefabs").Value;
            if (prefabs == null || !prefabs.Any()) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving Prefabs list, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Retrieved Prefabs list.");

            // Getting the original Landfill Prefab
            var lotPrefab = prefabs.FirstOrDefault(p => p.name == "Landfill Site Lot");
            if (lotPrefab == null) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving the original Landfill Prefab instance, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Retrieved the original Landfill Prefab instance.");

            // Getting the original Landfill Prefab's PlaceableNetData
            var lotPrefabData = prefabSystem.GetComponentData<PlaceableNetData>(lotPrefab);
            if (lotPrefabData.Equals(default(PlaceableNetData))) {
                // This type is not nullabe so we check equality with the default empty data
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving the original Landfill Prefab's PlaceableNetData instance, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader} Retrieved the original Landfill Prefab's PlaceableNetData instance.");

            // We now have all the needed original objects to patch our clones
            //foreach (var upgradeMode in ExtendedRoadUpgrades.Modes) {
            // Getting the cloned Landfill Prefab for the current upgrade mode
            var clonedLotPrefab = prefabs.FirstOrDefault(p => p.name == "ZONETEST");
            if (clonedLotPrefab == null) {
                Mod.Instance.Log.Error($"{logHeader} Failed retrieving the cloned Landfill Prefab instance, exiting.");
                return;
            }

            Mod.Instance.Log.Debug($"{logHeader}  Retrieved the cloned Landfill Prefab instance.");

            // // Getting the cloned Landfill Prefab's PlaceableNetData for the current upgrade mode
            //var clonedLandfillUpgradePrefabData = prefabSystem.GetComponentData<PlaceableNetData>(clonedLandfillUpgradePrefab);
            //if (clonedLandfillUpgradePrefabData.Equals(default(PlaceableNetData))) {
            //    // This type is not nullabe so we check equality with the default empty data
            //    Mod.Instance.Log.Error($"{logHeader} Failed retrieving the cloned Landfill Prefab's PlaceableNetData instance, exiting.");
            //    return;
            //}

            //    Mod.Instance.Log.Debug($"{logHeader} [{upgradeMode.Id}] Retrieved the cloned Landfill Prefab's PlaceableNetData instance.");

            //    // Update the flags with the ones set in our upgrade mode
            //    clonedLandfillUpgradePrefabData.m_SetUpgradeFlags = upgradeMode.m_SetUpgradeFlags;

            //    // TODO: this works even without the unset flags, keeping them there just in case
            //    clonedLandfillUpgradePrefabData.m_UnsetUpgradeFlags = upgradeMode.m_UnsetUpgradeFlags;

            //    // This toggles underground mode for our custom upgrade modes
            //    if (upgradeMode.IsUnderground) {
            //        clonedLandfillUpgradePrefabData.m_PlacementFlags |= Game.Net.PlacementFlags.UndergroundUpgrade;
            //    }

            //    // Persist the updated flags by replacing the ComponentData with the one we just created
            //    prefabSystem.AddComponentData(clonedLandfillUpgradePrefab, clonedLandfillUpgradePrefabData);

            //    Mod.Instance.Log.Info($"{logHeader} [{upgradeMode.Id}] Successfully set flags for our cloned Prefab to {clonedLandfillUpgradePrefabData.m_SetUpgradeFlags.ToJSONString()} and {clonedLandfillUpgradePrefabData.m_UnsetUpgradeFlags.ToJSONString()}.");
            //}

            // Mark the Prefix as already executed
            postInstalled = true;

            Mod.Instance.Log.Info($"{logHeader} Extended Road Upgrades Completed.");
        }
    }
}
