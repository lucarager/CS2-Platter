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
    using Colossal.UI;
    using Game;
    using Game.Input;
    using Game.Modding;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Newtonsoft.Json;
    using Platter.Components;
    using Platter.Patches;
    using Platter.Settings;
    using Platter.Systems;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;

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
        /// Gets the active instance reference.
        /// </summary>
        public static PlatterMod Instance {
            get; private set;
        }

        /// <summary>
        /// Gets the mod's active settings configuration.
        /// </summary>
        internal PlatterModSettings Settings {
            get; private set;
        }

        /// <summary>
        /// Gets the mod's active log.
        /// </summary>
        internal ILog Log {
            get; private set;
        }

        /// <inheritdoc/>
        public void OnLoad(UpdateSystem updateSystem) {
            // Set instance reference.
            Instance = this;

            // Initialize logger.
            Log = LogManager.GetLogger(ModName);
#if DEBUG
            Log.Info("[Platter] Setting logging level to Debug");
            Log.effectivenessLevel = Level.Debug;
#endif
            Log.Info($"[Platter] Loading {ModName} version {Assembly.GetExecutingAssembly().GetName().Version}");

            // Initialize Settings
            Settings = new(this);

            // Load i18n
            GameManager.instance.localizationManager.AddSource("en-US", new I18nConfig(Settings));
            Log.Info($"[Platter] Loaded en-US.");
            LoadNonEnglishLocalizations();
            Log.Info($"[Platter] Loaded localization files.");

            // Generate i18n files
#if DEBUG
            Log.Info($"[Platter] Exporting localization");
            var localeDict = new I18nConfig(Settings).ReadEntries(new List<IDictionaryEntryError>(), new Dictionary<string, int>()).ToDictionary(pair => pair.Key, pair => pair.Value);
            var str = JsonConvert.SerializeObject(localeDict, Newtonsoft.Json.Formatting.Indented);
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
#endif

            // Apply harmony patches.
            new Patcher("lucachoo-Platter", Log);

            // Register mod settings to game options UI.
            Settings.RegisterInOptionsUI();

            // Load saved settings.
            AssetDatabase.global.LoadSettings("Platter", Settings, new PlatterModSettings(this));

            // Inject additional settings config
            // ModifyMouseSettings(Settings);

            // Apply input bindings.
            Settings.RegisterKeyBindings();

            // Activate Systems
            updateSystem.UpdateAfter<PlatterPrefabSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ParcelInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<ParcelCreateSystem>(SystemUpdatePhase.Modification3);
            updateSystem.UpdateAt<ParcelSpawnSystem>(SystemUpdatePhase.Modification3);
            updateSystem.UpdateAt<ParcelUpdateSystem>(SystemUpdatePhase.Modification4);
            updateSystem.UpdateAt<RoadConnectionSystem>(SystemUpdatePhase.Modification4B);
            updateSystem.UpdateAt<ParcelToBlockReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<ParcelBlockToRoadReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<PlatterUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<SelectedInfoPanelSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<PlatterOverlaySystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<PlatterTooltipSystem>(SystemUpdatePhase.UITooltip);

            // Compatibility
            ModifyBuildingSystem(updateSystem.World.GetOrCreateSystemManaged<BuildingInitializeSystem>());

            // Add mod UI resource directory to UI resource handler.
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var modAsset)) {
                Log.Info($"Current mod asset at {modAsset.path}");
            }

            var assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
            UIManager.defaultUISystem.AddHostLocation("platter", assemblyPath + "/Assets/");
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

        private static void ModifyBuildingSystem(BuildingInitializeSystem originalSystem) {
            var log = LogManager.GetLogger(ModName);

            // get original system's EntityQuery
            FieldInfo queryField = originalSystem.GetType().GetField("m_PrefabQuery", BindingFlags.Instance | BindingFlags.NonPublic);
            if (queryField == null) {
                log.Error("Could not find m_PrefabQuery for compatibility patching");
                return;
            }

            EntityQuery originalQuery = (EntityQuery)queryField.GetValue(originalSystem);
            EntityQueryDesc[] originalQueryDescs = originalQuery.GetEntityQueryDescs();
            ComponentType componentType = ComponentType.ReadOnly<Parcel>();

            foreach (EntityQueryDesc originalQueryDesc in originalQueryDescs) {
                if (originalQueryDesc.None.Contains(componentType)) {
                    continue;
                }

                // add Parcel to force vanilla skip all entities with the Parcel component
                originalQueryDesc.None = originalQueryDesc.None.Append(componentType).ToArray();

                MethodInfo getQueryMethod = typeof(ComponentSystemBase).GetMethod("GetEntityQuery", BindingFlags.Instance | BindingFlags.NonPublic, null, CallingConventions.Any, new Type[] { typeof(EntityQueryDesc[]) }, Array.Empty<ParameterModifier>());

                // generate EntityQuery
                EntityQuery modifiedQuery = (EntityQuery)getQueryMethod.Invoke(originalSystem, new object[] { new EntityQueryDesc[] { originalQueryDesc } });

                // replace current query to use more restrictive
                queryField.SetValue(originalSystem, modifiedQuery);

                // add EntityQuery to update check
                originalSystem.RequireForUpdate(modifiedQuery);
            }
        }

        private static void ModifyMouseSettings(ModSetting originalSettings) {
            var log = LogManager.GetLogger(ModName);

            log.Debug(originalSettings.id);
            log.Debug(originalSettings.name);
            log.Debug(originalSettings.keyBindingRegistered);

            // get original system's EntityQuery
            // MemberInfo[] fields2 = originalSettings.GetType().BaseType.GetMembers(BindingFlags.NonPublic | BindingFlags.Instance);
            // foreach (MemberInfo mi in fields2) {
            //    Log.Debug($"2 {mi.Name} {mi.MemberType}");
            // }
            var keyBindingPropertiesPropArray = originalSettings.GetType().BaseType.GetProperty("keyBindingProperties", BindingFlags.Instance | BindingFlags.NonPublic);

            // Type objectType = typeof(PlatterModSettings);
            // PropertyInfo arrayProperty = objectType.BaseType.GetProperty("m_keyBindingProperties", BindingFlags.Instance | BindingFlags.NonPublic);
            // object arrayInstance = arrayProperty.GetValue(originalSettings);
            // System.Array targetArray = (System.Array)arrayInstance;

            // var device = InputManager.DeviceType.Mouse;
            // var actionName = "DecreaseParcelDepthActionName";
            // var actionType = ActionType.Vector2;
            // var component = ActionComponent.Up;
            // var control = "<Mouse>/scroll";
            // var enumerables = new string[] { "<Keyboard>/shift" };

            // var propEL = (PropertyInfo)targetArray.GetValue(3);
            // var prop = (PropertyInfo)propEL.GetValue(originalSettings);

            // var modifiers = enumerables.Select((string modifierControl) => new ProxyModifier {
            //    m_Component = component,
            //    m_Name = "modifier",
            //    m_Path = modifierControl
            // }).ToArray<ProxyModifier>();

            // var newBinding = new ProxyBinding(originalSettings.id, actionName, component, "binding", new CompositeInstance(device.ToString())) {
            //    device = device,
            //    path = control,
            //    originalPath = control,
            //    modifiers = modifiers,
            //    originalModifiers = modifiers
            // };

            // prop.SetValue(originalSettings, newBinding);
            // targetArray.SetValue(prop, 3);
            if (keyBindingPropertiesPropArray == null) {
                log.Error("Could not find keyBindingPropertiesPropArray for compatibility patching");
                return;
            }

            var keyBindingProperties = (PropertyInfo[])keyBindingPropertiesPropArray.GetValue(originalSettings);

            for (var i = 0; i < keyBindingProperties.Length; i++) {
                var keyBindingProperty = keyBindingProperties[i];
                var prop = (PropertyInfo)keyBindingProperty.GetValue(originalSettings);
                var binding = (ProxyBinding)keyBindingProperty.GetValue(originalSettings);

                log.Debug($"binding {binding}");
                log.Debug($"binding device {binding.device}");
                log.Debug($"binding component {binding.component}");
                log.Debug($"binding actionName {binding.actionName}");
                log.Debug($"binding mapName {binding.mapName}");
                if (binding.actionName == "DecreaseParcelDepthActionName") {
                    var device = InputManager.DeviceType.Mouse;
                    var actionName = binding.actionName;
                    var component = ActionComponent.Up;
                    var control = "<Mouse>/scroll";
                    var enumerables = new string[] { "<Keyboard>/shift" };

                    var modifiers = enumerables.Select((string modifierControl) => new ProxyModifier {
                        m_Component = component,
                        m_Name = "modifier",
                        m_Path = modifierControl
                    }).ToArray<ProxyModifier>();

                    var newBinding = new ProxyBinding(originalSettings.id, actionName, component, "binding", new CompositeInstance(device.ToString())) {
                        device = device,
                        path = control,
                        originalPath = control,
                        modifiers = modifiers,
                        originalModifiers = modifiers
                    };

                    log.Debug($"newBinding {newBinding.actionName}");
                    prop.SetValue(originalSettings, newBinding);
                }
            }
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
                            using System.IO.StreamReader reader = new(thisAssembly.GetManifestResourceStream(resourceName));
                            {
                                var entireFile = reader.ReadToEnd();
                                Colossal.Json.Variant varient = Colossal.Json.JSON.Load(entireFile);
                                Dictionary<string, string> translations = varient.Make<Dictionary<string, string>>();
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
