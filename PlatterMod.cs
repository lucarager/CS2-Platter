// <copyright file="PlatterMod.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter {
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Colossal.UI;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;
    using Platter.Patches;
    using Platter.Settings;
    using Platter.Systems;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Mod entry point.
    /// </summary>
    public class PlatterMod : IMod {
        /// <summary>
        /// The mod's default name.
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
        internal PlatterModSettings ActiveSettings {
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
            Log.Info("setting logging level to Debug");
            Log.effectivenessLevel = Level.Debug;
#endif
            Log.Info($"loading {ModName} version {Assembly.GetExecutingAssembly().GetName().Version}");

            // Initialize Settings
            ActiveSettings = new PlatterModSettings(this);

            // Load i18n
            GameManager.instance.localizationManager.AddSource("en-US", new I18nDictionary(ActiveSettings));

            // Apply harmony patches.
            new Patcher("lucachoo-Platter", Log);

            // Register mod settings to game options UI.
            ActiveSettings.RegisterInOptionsUI();

            // Load saved settings.
            AssetDatabase.global.LoadSettings("Platter", ActiveSettings, new PlatterModSettings(this));

            // Activate Systems
            updateSystem.UpdateAt<ParcelToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAfter<PrefabLoadSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ParcelInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<ParcelUpdateSystem>(SystemUpdatePhase.Modification4);
            updateSystem.UpdateAt<RoadConnectionSystem>(SystemUpdatePhase.Modification4B);
            updateSystem.UpdateAt<ParcelBlockReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<ParcelBlockRoadReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<ParcelToolUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<SelectedInfoPanelSystem>(SystemUpdatePhase.UIUpdate);

            // Add mod UI resource directory to UI resource handler.
            string assemblyName = Assembly.GetExecutingAssembly().FullName;
            ExecutableAsset modAsset = AssetDatabase.global.GetAsset(SearchFilter<ExecutableAsset>.ByCondition(
                x => x.definition?.FullName == assemblyName)
            );

            var assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
            Log.Info($"Loading assets from {assemblyPath}");
            UIManager.defaultUISystem.AddHostLocation("platter", assemblyPath + "/Icons/");
        }

        /// <inheritdoc/>
        public void OnDispose() {
            Log.Info("disposing");
            Instance = null;

            // Clear settings menu entry.
            if (ActiveSettings != null) {
                ActiveSettings.UnregisterInOptionsUI();
                ActiveSettings = null;
            }

            // Revert harmony patches.
            Patcher.Instance?.UnPatchAll();
        }
    }
}
