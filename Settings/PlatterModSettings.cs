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
    [SettingsUIMouseAction(IncreaseParcelWidthActionName, ActionType.Button, usages: new string[] { Usages.kToolUsage })]
    [SettingsUIMouseAction(DecreaseParcelWidthActionName, ActionType.Button, usages: new string[] { Usages.kToolUsage })]
    [SettingsUIMouseAction(IncreaseParcelDepthActionName, ActionType.Button, true, false, usages: new string[] { "Platter" })]
    [SettingsUIMouseAction(DecreaseParcelDepthActionName, ActionType.Button, true, false, usages: new string[] { "Platter" })]
    [SettingsUIKeyboardAction(DecreaseParcelDepthActionName, ActionType.Button, true, false, usages: new string[] { "Platter" })]
    public class PlatterModSettings : ModSetting {
        /// <summary>
        /// Tool's apply action name.
        /// </summary>
        public const string ToggleRenderActionName = "ToggleParcelRendering";

        /// <summary>
        /// Tool's apply action name.
        /// </summary>
        public const string ToggleSpawnActionName = "ToggleParcelSpawning";

        /// <summary>
        /// Tool's apply action name.
        /// </summary>
        public const string IncreaseParcelWidthActionName = "IncreaseParcelWidth";

        /// <summary>
        /// Tool's apply action name.
        /// </summary>
        public const string DecreaseParcelWidthActionName = "DecreaseParcelWidth";

        /// <summary>
        /// Tool's apply action name.
        /// </summary>
        public const string IncreaseParcelDepthActionName = "IncreaseParcelDepth";

        /// <summary>
        /// Tool's apply action name.
        /// </summary>
        public const string DecreaseParcelDepthActionName = "DecreaseParcelDepth";

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatterModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public PlatterModSettings(IMod mod)
            : base(mod) {
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.P, actionName: ToggleRenderActionName, ctrl: true)]
        public ProxyBinding PlatterToggleRender {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [SettingsUIKeyboardBinding(BindingKeyboard.P, actionName: ToggleSpawnActionName, ctrl: true)]
        public ProxyBinding PlatterToggleSpawn {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [CustomSettingsUIMouseBindingAttribute("<Mouse>/scroll/y", AxisComponent.Positive, IncreaseParcelWidthActionName, false, true, false)]
        public ProxyBinding PlatterIncreaseParcelWidth {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [CustomSettingsUIMouseBindingAttribute("<Mouse>/scroll/y", AxisComponent.Negative, DecreaseParcelWidthActionName, false, true, true)]
        public ProxyBinding PlatterDecreaseParcelWidth {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [CustomSettingsUIMouseBindingAttribute("<Mouse>/scroll/y", AxisComponent.Positive, IncreaseParcelDepthActionName, true, false, false)]
        public ProxyBinding PlatterIncreaseParcelDepth {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [CustomSettingsUIMouseBindingAttribute("<Mouse>/scroll/y", AxisComponent.Negative, DecreaseParcelDepthActionName, true, false, true)]
        public ProxyBinding PlatterDecreaseParcelDepth {
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
