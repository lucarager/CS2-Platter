// <copyright file="PlatterModSettings.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Settings {
    using Colossal.IO.AssetDatabase;
    using Game;
    using Game.Input;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.Settings;

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(PlatterMod.ModName)]
    [SettingsUIMouseAction(ApplyActionName, ActionType.Button, false, false, new string[] { "PlatterTool" })]
    [SettingsUIKeyboardAction(CreateActionName, ActionType.Button, false, false, usages: new string[] { "PlatterTool" })]
    [SettingsUIMouseAction(CancelActionName, ActionType.Button, false, false, usages: new string[] { "PlatterTool" })]
    public class PlatterModSettings : ModSetting {
        /// <summary>
        /// Tool's apply action name.
        /// </summary>
        internal const string ApplyActionName = "PlatterToolApply";

        /// <summary>
        /// Tool's apply action name.
        /// </summary>
        internal const string CreateActionName = "PlatterToolCreate";

        /// <summary>
        /// Tool's apply action name.
        /// </summary>
        internal const string CancelActionName = "PlatterToolCancel";

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatterModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public PlatterModSettings(IMod mod)
            : base(mod) {
        }

        /// <summary>
        /// Gets or sets the Platter Tool apply action (copied from game action).
        /// </summary>
        [SettingsUIMouseBinding(ApplyActionName)]
        [SettingsUIBindingMimic(InputManager.kToolMap, "Apply")]
        public ProxyBinding PlatterToolApply {
            get; set;
        }

        /// <summary>
        /// Gets or sets the Platter Tool apply action (copied from game action).
        /// </summary>
        [SettingsUIKeyboardBinding(CreateActionName)]
        public ProxyBinding PlatterToolCreate {
            get; set;
        }

        /// <summary>
        /// Gets or sets the Platter Tool cancel action (copied from game action).
        /// </summary>
        [SettingsUIMouseBinding(CancelActionName)]
        [SettingsUIBindingMimic(InputManager.kToolMap, "Cancel")]
        public ProxyBinding PlatterToolCancel {
            get; set;
        }

        /// <summary>
        /// Restores mod settings to default.
        /// </summary>
        public override void SetDefaults() {
        }

        /// <summary>
        /// Determines whether we're currently in-game (in a city) or not.
        /// </summary>
        /// <returns><c>false</c> if we're currently in-game, <c>true</c> otherwise (such as in the main menu or editor).</returns>
        public bool IsNotInGame() {
            return GameManager.instance.gameMode != GameMode.Game;
        }
    }
}
