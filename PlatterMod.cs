// <copyright file="PlatterMod.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Colossal;
    using Colossal.IO.AssetDatabase;
    using Colossal.Localization;
    using Colossal.Logging;
    using Colossal.Reflection;
    using Colossal.TestFramework;
    using Colossal.UI;
    using Game;
    using Game.Modding;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.Serialization;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using Newtonsoft.Json;
    using Platter.Components;
    using Platter.Patches;
    using Platter.Settings;
    using Platter.Systems;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Mod entry point.
    /// </summary>
    public class PlatterMod : IMod {
        /// <summary>
        /// The mod's default actionName.
        /// </summary>
        public const string ModName = "Platter";

        /// <summary>
        /// An id used for bindings between UI and C#.
        /// </summary>
        public static readonly string Id = "Platter";

        /// <summary>
        /// Gets the instance reference.
        /// </summary>
        public static PlatterMod Instance {
            get; private set;
        }

        /// <summary>
        /// Gets the mod's settings configuration.
        /// </summary>
        internal PlatterModSettings Settings {
            get; private set;
        }

        /// <summary>
        /// Gets the mod's logger.
        /// </summary>
        internal ILog Log {
            get; private set;
        }

        /// <summary>
        /// Gets the mod's version
        /// </summary>
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString(4);

        /// <summary>
        /// Gets the mod's informational version
        /// </summary>
        public static string InformationalVersion => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        /// <inheritdoc/>
        public void OnLoad(UpdateSystem updateSystem) {
            // Set instance reference.
            Instance = this;

            // Initialize logger.
            Log = LogManager.GetLogger(ModName);
#if IS_DEBUG
            Log.Info("[Platter] Setting logging level to Debug");
            Log.effectivenessLevel = Level.Debug;
#endif
            Log.Info($"[Platter] Loading {ModName} version {Assembly.GetExecutingAssembly().GetName().Version}");

            // Initialize Settings
            Settings = new PlatterModSettings(this);

            // Load i18n
            GameManager.instance.localizationManager.AddSource("en-US", new EnUsConfig(Settings));
            Log.Info($"[Platter] Loaded en-US.");
            LoadNonEnglishLocalizations();
            Log.Info($"[Platter] Loaded localization files.");

            // Generate i18n files
#if IS_DEBUG && EXPORT_EN_US
            GenerateLanguageFile();
#endif

            // Apply harmony patches.
            // ReSharper disable once ObjectCreationAsStatement
            new Patcher("lucachoo-Platter", Log);

            // Apply inflection patches.
            ModifyVabillaSubBlockSerialization(updateSystem.World.GetOrCreateSystemManaged<SubBlockSystem>());

            // Register mod settings to game options UI.
            Settings.RegisterInOptionsUI();

            // Load saved settings.
            AssetDatabase.global.LoadSettings("Platter", Settings, new PlatterModSettings(this));

            // Apply input bindings.
            Settings.RegisterKeyBindings();

            // Activate Systems
            // Serializaztion/Deserializaztion
            updateSystem.UpdateBefore<PreDeserialize<P_ParcelSearchSystem>>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_ParcelSubBlockDeserializeSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_ConnectedParcelDeserializeSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_ConnectedParcelLoadSystem>(SystemUpdatePhase.Deserialize);

            // Prefabs
            updateSystem.UpdateAfter<P_PrefabsCreateSystem, ObjectInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<P_ParcelInitializeSystem, ObjectInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<P_ZoneCacheSystem>(SystemUpdatePhase.PrefabUpdate);

            // Buildings
            updateSystem.UpdateAfter<P_BuildingInitializeSystem, BuildingConstructionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<P_BuildingTransformCheckSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<P_BuildingToParcelReferenceSystem>(SystemUpdatePhase.Modification2);

            // Parcels
            updateSystem.UpdateAt<P_ParcelCreateSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<P_AllowSpawnSystem>(SystemUpdatePhase.Modification3);
            updateSystem.UpdateAt<P_ConnectedParcelCreateSystem>(SystemUpdatePhase.Modification4);
            updateSystem.UpdateAt<P_ParcelUpdateSystem>(SystemUpdatePhase.Modification4);
            updateSystem.UpdateAt<P_RoadConnectionSystem>(SystemUpdatePhase.Modification4B);
            updateSystem.UpdateAt<P_ParcelToBlockReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<P_BlockToRoadReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<P_ParcelSearchSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAfter<P_BlockUpdateSystem>(SystemUpdatePhase.Modification5); // Needs to run after CellCheckSystem

            // UI/Rendering
            updateSystem.UpdateAt<P_UISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<P_ParcelInfoPanelSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<P_BuildingInfoPanelSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<P_OverlaySystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<P_TooltipSystem>(SystemUpdatePhase.UITooltip);

            // Tools
            updateSystem.UpdateBefore<P_SnapSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateBefore<P_GenerateZonesSystem, GenerateZonesSystem>(SystemUpdatePhase.Modification1); // Needs to run before GenerateZonesSystem
            updateSystem.UpdateAt<P_CellCheckSystem>(SystemUpdatePhase.ModificationEnd);
            //updateSystem.UpdateAt<P_TestToolSystem>(SystemUpdatePhase.ToolUpdate);
            
            // Experimental Systems
            //updateSystem.UpdateAfter<P_BuildingPrefabClassifySystem>(SystemUpdatePhase.Modification1);
            //updateSystem.UpdateAfter<P_BuildingSpawnSystem>(SystemUpdatePhase.GameSimulation);

            // Add tests
            AddTests();

            // Add mod UI resource directory to UI resource handler.
            if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out var modAsset)) {
                Log.Error($"Failed to get executable asset path. Exiting.");
                return;
            }

            var assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
            UIManager.defaultUISystem.AddHostLocation("platter", assemblyPath + "/Assets/");

            Log.Info($"Installed and enabled. RenderedFrame: {Time.renderedFrameCount}");
        }

        private void GenerateLanguageFile() {
            Log.Info($"[Platter] Exporting localization");
            var localeDict = new EnUsConfig(Settings).ReadEntries(new List<IDictionaryEntryError>(), new Dictionary<string, int>()).ToDictionary(pair => pair.Key, pair => pair.Value);
            var str        = JsonConvert.SerializeObject(localeDict, Newtonsoft.Json.Formatting.Indented);
            try {
                var path = "C:\\Users\\lucar\\source\\repos\\Platter\\lang\\en-US.json";
                Log.Info($"[Platter] Exporting to {path}");
                File.WriteAllText(path, str);
                path = "C:\\Users\\lucar\\source\\repos\\Platter\\UI\\src\\lang\\en-US.json";
                Log.Info($"[Platter] Exporting to {path}");
                File.WriteAllText(path, str);
            } catch (Exception ex) {
                Log.Error(ex.ToString());
            }
        }

        /// <inheritdoc/>
        public void OnDispose() {
            Log.Info("[Platter] Disposing");
            Instance = null;

            // Clear settings menu entry.
            if (Settings != null) {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }

            // Revert harmony patches.
            Patcher.Instance?.UnPatchAll();
        }

        private static void AddTests() {
            var log = LogManager.GetLogger(ModName);
            log.Debug($"AddTests()");

            var m_ScenariosField = typeof(TestScenarioSystem).GetField("m_Scenarios", BindingFlags.Instance | BindingFlags.NonPublic);
            if (m_ScenariosField == null) {
                log.Error("AddTests() -- Could not find m_Scenarios");
                return;
            }

            var m_Scenarios = (Dictionary<string, TestScenarioSystem.Scenario>)m_ScenariosField.GetValue(TestScenarioSystem.instance);

            foreach (var type in GetTests()) {
                if (!type.IsClass || type.IsAbstract || type.IsInterface || !type.TryGetAttribute(
                        out TestDescriptorAttribute testDescriptorAttribute, false)) {
                    continue;
                }

                log.Debug($"AddTests() -- {testDescriptorAttribute.description}");

                m_Scenarios.Add(testDescriptorAttribute.description, new TestScenarioSystem.Scenario {
                    category  = testDescriptorAttribute.category,
                    testPhase = testDescriptorAttribute.testPhase,
                    test      = type,
                    disabled  = testDescriptorAttribute.disabled,
                });
            }

            m_Scenarios = TestScenarioSystem.SortScenarios(m_Scenarios);

            m_ScenariosField.SetValue(TestScenarioSystem.instance, m_Scenarios);
        }

        private static IEnumerable<Type> GetTests() {
            return from t in Assembly.GetExecutingAssembly().GetTypes()
                   where typeof(TestScenario).IsAssignableFrom(t)
                   select t;
        }

        private static void ModifyVabillaSubBlockSerialization(ComponentSystemBase originalSystem) {
            var log = LogManager.GetLogger(ModName);
            log.Debug("ModifyVabillaSubBlockSerialization()");

            // get original system's EntityQuery
            var queryField = typeof(SubBlockSystem).GetField("m_Query", BindingFlags.Instance | BindingFlags.NonPublic);
            if (queryField == null) {
                log.Error("ModifyVabillaSubBlockSerialization() -- Could not find m_Query for compatibility patching");
                return;
            }

            var originalQuery = (EntityQuery)queryField.GetValue(originalSystem);

            if (originalQuery.GetHashCode() == 0) {
                log.Error("ModifyVabillaSubBlockSerialization() -- SubBlockSystem was not initialized!");
            }

            var originalQueryDescs = originalQuery.GetEntityQueryDescs();
            var componentType = ComponentType.ReadOnly<ParcelOwner>();

            foreach (var originalQueryDesc in originalQueryDescs) {
                if (originalQueryDesc.None.Contains(componentType)) {
                    continue;
                }

                // add Parcel to force vanilla skip all entities with the Parcel component
                originalQueryDesc.None = originalQueryDesc.None.Append(componentType).ToArray();
                var getQueryMethod = typeof(ComponentSystemBase).GetMethod(
                    "GetEntityQuery",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    CallingConventions.Any,
                    new Type[] { typeof(EntityQueryDesc[]) },
                    Array.Empty<ParameterModifier>()
                 );

                // generate EntityQuery
                var modifiedQuery = (EntityQuery)getQueryMethod.Invoke(originalSystem, new object[] { new EntityQueryDesc[] { originalQueryDesc } });

                // replace current query to use more restrictive
                queryField.SetValue(originalSystem, modifiedQuery);

                // add EntityQuery to update check
                originalSystem.RequireForUpdate(modifiedQuery);
            }

            log.Debug("ModifyVabillaSubBlockSerialization() -- Patching complete.");
        }

        private void LoadNonEnglishLocalizations() {
            var thisAssembly = Assembly.GetExecutingAssembly();
            var resourceNames = thisAssembly.GetManifestResourceNames();

            try {
                Log.Debug($"Reading localizations");

                foreach (var localeID in GameManager.instance.localizationManager.GetSupportedLocales()) {
                    var resourceName = $"{thisAssembly.GetName().Name}.lang.{localeID}.json";
                    if (resourceNames.Contains(resourceName)) {
                        Log.Debug($"Found localization file {resourceName}");
                        try {
                            Log.Debug($"Reading embedded translation file {resourceName}");

                            // Read embedded file.
                            using System.IO.StreamReader reader = new (thisAssembly.GetManifestResourceStream(resourceName));
                            {
                                var entireFile = reader.ReadToEnd();
                                var varient = Colossal.Json.JSON.Load(entireFile);
                                var translations = varient.Make<Dictionary<string, string>>();
                                GameManager.instance.localizationManager.AddSource(localeID, new MemorySource(translations));
                            }
                        } catch (Exception e) {
                            // Don't let a single failure stop us.
                            Log.Error(e, $"Exception reading localization from embedded file {resourceName}");
                        }
                    } else {
                        Log.Debug($"Did not find localization file {resourceName}");
                    }
                }
            } catch (Exception e) {
                Log.Error(e, "Exception reading embedded settings localization files");
            }
        }
    }
}
