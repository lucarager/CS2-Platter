// <copyright file="PlatterMod.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using Colossal;
    using Colossal.IO.AssetDatabase;
    using Colossal.Json;
    using Colossal.Localization;
    using Colossal.Logging;
    using Colossal.Reflection;
    using Colossal.TestFramework;
    using Colossal.UI;
    using Components;
    using Extensions;
    using Game;
    using Game.Input;
    using Game.Modding;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.Serialization;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using HarmonyLib;
    using Newtonsoft.Json;
    using Settings;
    using Systems;
    using Unity.Entities;
    using UnityEngine;
    using Utils;
    using StreamReader = System.IO.StreamReader;

    #endregion

    /// <summary>
    /// Mod entry point.
    /// </summary>
    public class PlatterMod : IMod {
        /// <summary>
        /// An id used for bindings between UI and C#.
        /// </summary>
        public const string Id = "Platter";

        /// <summary>
        /// The mod's default actionName.
        /// </summary>
        public const string ModName = "Platter";

        private static readonly string HarmonyPatchId = $"{nameof(Platter)}.{nameof(PlatterMod)}";

        private Harmony        m_Harmony;
        private PrefixedLogger m_Log;

        /// <summary>
        /// Sets mod to test mode
        /// </summary>
        internal bool IsTestMode { get; set; } = false;

        /// <summary>
        /// Gets the mod's logger.
        /// </summary>
        internal ILog Log { get; private set; }

        /// <summary>
        /// Gets the instance reference.
        /// </summary>
        public static PlatterMod Instance { get; private set; }

        /// <summary>
        /// Gets the mod's settings configuration.
        /// </summary>
        internal PlatterModSettings Settings { get; private set; }

        /// <summary>
        /// Gets the mod's informational version
        /// </summary>
        public static string InformationalVersion =>
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        /// <summary>
        /// Gets the mod's version
        /// </summary>
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString(4);

        /// <inheritdoc/>
        public void OnLoad(UpdateSystem updateSystem) {
            Instance = this;

            // Initialize logger.
            Log   = LogManager.GetLogger(ModName);
            m_Log = new PrefixedLogger(nameof(PlatterMod));
            m_Log.Info($"Loading {ModName} version {Assembly.GetExecutingAssembly().GetName().Version}");

            InitializeSettings();
#if IS_DEBUG && EXPORT_EN_US
            GenerateLanguageFile();
#endif
            ModifyVabillaSubBlockSerialization(updateSystem.World.GetOrCreateSystemManaged<SubBlockSystem>());
            InitializeHarmonyPatches();

            RegisterSystems(updateSystem);
#if IS_DEBUG
            AddTests();
#endif

            if (RegisterAssets()) {
                return;
            }

            m_Log.Info($"Installed and enabled. RenderedFrame: {Time.renderedFrameCount}");
        }

        /// <inheritdoc/>
        public void OnDispose() {
            m_Log.Info("OnDispose()");
            Instance = null;

            if (Settings != null) {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }

            TeardownHarmonyPatches();
        }

        private bool RegisterAssets() {
            m_Log.Debug("RegisterAssets()");
            if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out var modAsset)) {
                m_Log.Error("Failed to get executable asset path. Exiting.");
                return true;
            }

            var assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
            UIManager.defaultUISystem.AddHostLocation("platter", assemblyPath + "/Assets/");
            return false;
        }

        private void RegisterSystems(UpdateSystem updateSystem) {
            m_Log.Debug("RegisterSystems()");

            // Serializaztion/Deserializaztion
            updateSystem.UpdateBefore<PreDeserialize<P_ParcelSearchSystem>>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_ParcelSubBlockDeserializeSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_ConnectedParcelDeserializeSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_ConnectedParcelLoadSystem>(SystemUpdatePhase.Deserialize);

            // Prefabs
            updateSystem.UpdateAfter<P_PrefabsCreateSystem, ObjectInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<P_ParcelInitializeSystem, ObjectInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<P_ZoneCacheSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<P_BuildingCacheSystem>(SystemUpdatePhase.PrefabUpdate);

            // Buildings
            updateSystem.UpdateAfter<P_BuildingInitializeSystem, BuildingConstructionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<P_BuildingTransformCheckSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<P_BuildingToParcelReferenceSystem>(SystemUpdatePhase.Modification2);

            // Roads
            updateSystem.UpdateAt<P_ConnectedParcelSystem>(SystemUpdatePhase.Modification1);

            // Parcels
            updateSystem.UpdateAt<P_PlaceholderSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<P_ParcelUpdateSystem>(SystemUpdatePhase.Modification2);
            updateSystem.UpdateAt<P_AllowSpawnSystem>(SystemUpdatePhase.Modification3);
            updateSystem.UpdateAt<P_RoadConnectionSystem>(SystemUpdatePhase.Modification4B);
            updateSystem.UpdateAt<P_ParcelToBlockReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<P_BlockToRoadReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<P_ParcelSearchSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateBefore<P_RemoveOverriddenSystem>(SystemUpdatePhase.ModificationEnd); // Run after Mod5 when overrides are applied
            updateSystem.UpdateAt<P_ParcelBlockClassifySystem>(SystemUpdatePhase.ModificationEnd); 

            // UI/Rendering
            updateSystem.UpdateAt<P_UISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<P_ParcelInfoPanelSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<P_BuildingInfoPanelSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<P_OverlaySystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<P_TooltipSystem>(SystemUpdatePhase.UITooltip);

            // Tools
            updateSystem.UpdateBefore<P_SnapSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateBefore<P_GenerateZonesSystem, GenerateZonesSystem>(SystemUpdatePhase.Modification1); // Needs to run before GenerateZonesSystem
            updateSystem.UpdateAt<P_TestToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateBefore<P_CellCheckSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<P_BlockUpdateSystem>(SystemUpdatePhase.ModificationEnd); // Needs to run after CellCheckSystem
        }

        private void InitializeSettings() {
            m_Log.Debug("InitializeSettings()");
            RegisterCustomInputActions();
            Settings = new PlatterModSettings(this);
            Settings.RegisterInOptionsUI();
            AssetDatabase.global.LoadSettings("Platter", Settings, new PlatterModSettings(this));
            Settings.RegisterKeyBindings();
            GameManager.instance.localizationManager.AddSource("en-US", new EnUsConfig(Settings));
            LoadNonEnglishLocalizations();
        }

        private void RegisterCustomInputActions() {
            m_Log.Debug("RegisterCustomInputActions()");

            RegisterCustomScrollAction("BlockDepthAction", new Tuple<string, string>[] {
                new Tuple<string, string>("ctrl", "<Keyboard>/ctrl"),
            });
            RegisterCustomScrollAction("BlockWidthAction", new Tuple<string, string>[] {
                new Tuple<string, string>("alt", "<Keyboard>/alt"),
            });
            RegisterCustomScrollAction("BlockSizeAction", new Tuple<string, string>[] {
                new Tuple<string, string>("alt", "<Keyboard>/alt"),
                new Tuple<string, string>("ctrl", "<Keyboard>/ctrl"),
            });
            RegisterCustomScrollAction("SetbackAction", new Tuple<string, string>[] {
                new Tuple<string, string>("ctrl", "<Keyboard>/ctrl"),
                new Tuple<string, string>("shift", "<Keyboard>/shift"),
            });
        }

        private void RegisterCustomScrollAction(string name, Tuple<string, string>[] modifiers) {
            var preciseRotation = InputManager.instance.FindAction("Tool", "Precise Rotation");
            if (preciseRotation == null) {
                m_Log.Error("RegisterCustomInputActions() -- Could not find Precise Rotation action");
                return;
            }

            var composites = preciseRotation.composites;

            var blockWidthCustomAction = new ProxyAction.Info {
                m_Name = name,
                m_Map  = "Platter",
                m_Type = ActionType.Vector2,
                m_Composites = composites.Select(keyValuePair => {
                    var device     = keyValuePair.Key;
                    var composite  = keyValuePair.Value;
                    var source     = (CompositeInstance)composite.GetMemberValue("m_Source");
                    source.builtIn = false;

                    // Get the original bindings and modify them to use Alt modifier only
                    var modifiedBindings = composite.bindings.Values.Select(binding => {
                        return binding.WithModifiers(modifiers.Select(m => new ProxyModifier {
                            m_Component = binding.component,
                            m_Name      = m.Item1,
                            m_Path      = m.Item2,
                        }).ToList());
                    }).ToList();

                    return new ProxyComposite.Info {
                        m_Device   = device,
                        m_Source   = source,
                        m_Bindings = modifiedBindings,
                    };
                }).ToList(),
            };

            // Use reflection extension to call the internal AddActions instance method
            if (!InputManager.instance.TryInvokeMethod("AddActions", out _, new[] { blockWidthCustomAction })) {
                m_Log.Error("RegisterCustomInputActions() -- Could not find or invoke InputManager.AddActions method");
                return;
            }

            m_Log.Debug($"RegisterCustomInputActions() -- Registered custom action {name}");
        }

        private void InitializeHarmonyPatches() {
            m_Log.Debug("InitializeHarmonyPatches()");

            m_Harmony = new Harmony(HarmonyPatchId);
            m_Harmony.PatchAll(typeof(PlatterMod).Assembly);
            var patchedMethods = m_Harmony.GetPatchedMethods().ToArray();

            foreach (var patchedMethod in patchedMethods) {
                m_Log.Debug($"InitializeHarmonyPatches() -- Patched method: {patchedMethod.Module.ScopeName}:{patchedMethod.Name}");
            }
        }

        private void TeardownHarmonyPatches() {
            m_Log.Debug("TeardownHarmonyPatches()");
            m_Harmony.UnpatchAll(HarmonyPatchId);
        }

        private void GenerateLanguageFile() {
            m_Log.Debug("GenerateLanguageFile()");
            var localeDict = new EnUsConfig(Settings).ReadEntries(new List<IDictionaryEntryError>(), new Dictionary<string, int>())
                                                     .ToDictionary(pair => pair.Key, pair => pair.Value);
            var str = JsonConvert.SerializeObject(localeDict, Formatting.Indented);
            try {
                var path      = GetThisFilePath();
                var directory = Path.GetDirectoryName(path);

                var exportPath1 = $@"{directory}\lang\en-US.json";
                var exportPath2 = $@"{directory}\UI\src\lang\en-US.json";
                File.WriteAllText(exportPath1, str);
                File.WriteAllText(exportPath2, str);
            } catch (Exception ex) {
                m_Log.Error(ex.ToString());
            }
        }

        private static string GetThisFilePath([CallerFilePath] string path = null) { return path; }

        private void AddTests() {
            m_Log.Info("AddTests()");

            var log = LogManager.GetLogger(ModName);

            var field = typeof(TestScenarioSystem).GetField("m_Scenarios", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                log.Error("AddTests() -- Could not find m_Scenarios");
                return;
            }

            var m_Scenarios = (Dictionary<string, TestScenarioSystem.Scenario>)field.GetValue(TestScenarioSystem.instance);

            foreach (var type in GetTests()) {
                if (!type.IsClass || type.IsAbstract || type.IsInterface || !type.TryGetAttribute(
                        out TestDescriptorAttribute testDescriptorAttribute)) {
                    continue;
                }

                log.Debug($"AddTests() -- {testDescriptorAttribute.description}");

                m_Scenarios.Add(
                    testDescriptorAttribute.description,
                    new TestScenarioSystem.Scenario {
                        category  = testDescriptorAttribute.category,
                        testPhase = testDescriptorAttribute.testPhase,
                        test      = type,
                        disabled  = testDescriptorAttribute.disabled,
                    });
            }

            m_Scenarios = TestScenarioSystem.SortScenarios(m_Scenarios);

            field.SetValue(TestScenarioSystem.instance, m_Scenarios);
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
            var componentType      = ComponentType.ReadOnly<ParcelOwner>();

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
                    new[] { typeof(EntityQueryDesc[]) },
                    Array.Empty<ParameterModifier>()
                );

                // generate EntityQuery
                var modifiedQuery = (EntityQuery)getQueryMethod.Invoke(originalSystem, new object[] { new[] { originalQueryDesc } });

                // replace current query to use more restrictive
                queryField.SetValue(originalSystem, modifiedQuery);

                // add EntityQuery to update check
                originalSystem.RequireForUpdate(modifiedQuery);
            }

            log.Debug("ModifyVabillaSubBlockSerialization() -- Patching complete.");
        }

        private void LoadNonEnglishLocalizations() {
            var thisAssembly  = Assembly.GetExecutingAssembly();
            var resourceNames = thisAssembly.GetManifestResourceNames();

            try {
                m_Log.Debug("Reading localizations");

                foreach (var localeID in GameManager.instance.localizationManager.GetSupportedLocales()) {
                    var resourceName = $"{thisAssembly.GetName().Name}.lang.{localeID}.json";
                    if (resourceNames.Contains(resourceName)) {
                        m_Log.Debug($"Found localization file {resourceName}");
                        try {
                            m_Log.Debug($"Reading embedded translation file {resourceName}");

                            // Read embedded file.
                            using StreamReader reader = new(thisAssembly.GetManifestResourceStream(resourceName));
                            {
                                var entireFile   = reader.ReadToEnd();
                                var varient      = JSON.Load(entireFile);
                                var translations = varient.Make<Dictionary<string, string>>();
                                GameManager.instance.localizationManager.AddSource(localeID, new MemorySource(translations));
                            }
                        } catch (Exception e) {
                            // Don't let a single failure stop us.
                            m_Log.Error($"Exception reading localization from embedded file {resourceName}");
                        }
                    } else {
                        m_Log.Debug($"Did not find localization file {resourceName}");
                    }
                }
            } catch (Exception e) {
                m_Log.Error($"Exception reading embedded settings localization files {e}");
            }
        }
    }
}