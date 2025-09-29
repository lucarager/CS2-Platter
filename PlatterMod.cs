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
    using Game.SceneFlow;
    using Newtonsoft.Json;
    using Platter.Patches;
    using Platter.Settings;
    using Platter.Systems;
    using Unity.Collections.LowLevel.Unsafe;

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
#if DEBUG && EXPORT_EN_US
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
            updateSystem.UpdateAt<VanillaRoadInitializeSystem>(SystemUpdatePhase.Modification4);
            updateSystem.UpdateAt<ParcelUpdateSystem>(SystemUpdatePhase.Modification4);
            updateSystem.UpdateAt<RoadConnectionSystem>(SystemUpdatePhase.Modification4B);
            updateSystem.UpdateAt<ParcelToBlockReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<ParcelBlockToRoadReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<PlatterUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<SelectedInfoPanelSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<PlatterOverlaySystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<PlatterTooltipSystem>(SystemUpdatePhase.UITooltip);

            // Add mod UI resource directory to UI resource handler.
            var assemblyName = Assembly.GetExecutingAssembly().FullName;
            if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out var modAsset)) {
                Log.Error($"Failed to get executable asset path. Exiting.");
                return;
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
