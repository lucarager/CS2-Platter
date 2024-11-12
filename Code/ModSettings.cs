namespace Platter
{
    using Colossal.IO.AssetDatabase;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.Settings;

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(Mod.ModName)]
    public class ModSettings : ModSetting
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public ModSettings(IMod mod)
            : base(mod)
        {
        }

        /// <summary>
        /// Restores mod settings to default.
        /// </summary>
        public override void SetDefaults()
        {
        }

        /// <summary>
        /// Determines whether we're currently in-game (in a city) or not.
        /// </summary>
        /// <returns><c>false</c> if we're currently in-game, <c>true</c> otherwise (such as in the main menu or editor).</returns>
        public bool IsNotInGame() => GameManager.instance.gameMode != GameMode.Game;
    }

}
