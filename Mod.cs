namespace Platter
{
    using System.Reflection;
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Game;
    using Game.Input;
    using Game.Modding;
    using Game.Prefabs;
    using Platter.Patches;
    using Platter.Systems;

    public class Mod : IMod
    {
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
        public static Mod Instance { get; private set; }

        /// <summary>
        /// Gets the mod's active settings configuration.
        /// </summary>
        internal ModSettings ActiveSettings { get; private set; }

        /// <summary>
        /// Gets the mod's active log.
        /// </summary>
        internal ILog Log { get; private set; }

        // Demo stuff
        public static ProxyAction m_ButtonAction;
        public static ProxyAction m_AxisAction;
        public static ProxyAction m_VectorAction;
        public const string kButtonActionName = "ButtonBinding";
        public const string kAxisActionName = "FloatBinding";
        public const string kVectorActionName = "Vector2Binding";


        /// <inheritdoc/>
        public void OnLoad(UpdateSystem updateSystem)
        {
            // Set instance reference.
            Instance = this;

            // Initialize logger.
            Log = LogManager.GetLogger(ModName);
#if DEBUG
            Log.Info("setting logging level to Debug");
            Log.effectivenessLevel = Level.Debug;
#endif
            Log.Info($"loading {ModName} version {Assembly.GetExecutingAssembly().GetName().Version}");

            // Apply harmony patches.
            new Patcher("lucachoo-Platter", Log);

            // Register mod settings to game options UI.
            ActiveSettings = new ModSettings(this);
            ActiveSettings.RegisterInOptionsUI();

            // Load saved settings.
            AssetDatabase.global.LoadSettings("Platter", ActiveSettings, new ModSettings(this));

            // Activate Systems
            updateSystem.UpdateAfter<TestSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ParcelInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<ParcelBlockSpawnSystem>(SystemUpdatePhase.Modification4);
            updateSystem.UpdateAt<ParcelConnectionSystem>(SystemUpdatePhase.Modification4B);
            updateSystem.UpdateAt<ParcelBlockReferenceSystem>(SystemUpdatePhase.Modification5);
        }

        /// <inheritdoc/>
        public void OnDispose()
        {
            Log.Info("disposing");
            Instance = null;

            // Clear settings menu entry.
            if (ActiveSettings != null)
            {
                ActiveSettings.UnregisterInOptionsUI();
                ActiveSettings = null;
            }

            // Revert harmony patches.
            Patcher.Instance?.UnPatchAll();
        }
    }
}
