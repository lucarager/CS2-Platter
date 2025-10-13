// <copyright file="PlatterModSettings.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Settings {
    using System;
    using Colossal.IO.AssetDatabase;
    using Game;
    using Game.Input;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.Settings;
    using Platter.Systems;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(PlatterMod.ModName)]
    [SettingsUIMouseAction(IncreaseParcelWidthActionName, ActionType.Button, usages: new string[] { Usages.kToolUsage })]
    [SettingsUIMouseAction(DecreaseParcelWidthActionName, ActionType.Button, usages: new string[] { Usages.kToolUsage })]
    [SettingsUIMouseAction(IncreaseParcelDepthActionName, ActionType.Button, true, false, usages: new string[] { "Platter" })]
    [SettingsUIMouseAction(DecreaseParcelDepthActionName, ActionType.Button, true, false, usages: new string[] { "Platter" })]
    [SettingsUIKeyboardAction(DecreaseParcelDepthActionName, ActionType.Button, true, false, usages: new string[] { "Platter" })]
    [SettingsUIGroupOrder(KeybindingsGroup, UninstallGroup, AboutGroup)]
    [SettingsUIShowGroupName(KeybindingsGroup, UninstallGroup, AboutGroup)]
    public class PlatterModSettings : ModSetting {
        public const string KeybindingsGroup = "KeybindingsGroup";
        public const string UninstallGroup = "UninstallGroup";
        public const string AboutGroup = "AboutGroup";
        public const string ToggleRenderActionName = "ToggleRenderActionName";
        public const string ToggleSpawnActionName = "ToggleSpawnActionName";
        public const string IncreaseParcelWidthActionName = "IncreaseParcelWidthActionName";
        public const string DecreaseParcelWidthActionName = "DecreaseParcelWidthActionName";
        public const string IncreaseParcelDepthActionName = "IncreaseParcelDepthActionName";
        public const string DecreaseParcelDepthActionName = "DecreaseParcelDepthActionName";
        private const string Credit = "Made with <3 by Luca.";

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
        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.P, actionName: ToggleRenderActionName, ctrl: true)]
        public ProxyBinding PlatterToggleRender {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.L, actionName: ToggleSpawnActionName, ctrl: true)]
        public ProxyBinding PlatterToggleSpawn {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Z, actionName: IncreaseParcelWidthActionName, ctrl: true)]
        public ProxyBinding PlatterIncreaseParcelWidth {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.X, actionName: DecreaseParcelWidthActionName, ctrl: true)]
        public ProxyBinding PlatterDecreaseParcelWidth {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Z, actionName: IncreaseParcelDepthActionName, alt: true)]
        public ProxyBinding PlatterIncreaseParcelDepth {
            get; set;
        }

        /// <summary>
        /// Gets or sets ...
        /// </summary>
        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.X, actionName: DecreaseParcelDepthActionName, alt: true)]
        public ProxyBinding PlatterDecreaseParcelDepth {
            get; set;
        }

        [SettingsUISection(UninstallGroup)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUIDisableByCondition(typeof(PlatterModSettings), nameof(IsNotInGame))]
        public bool RemoveParcels {
            set {
                var uninstallSystem = World.DefaultGameObjectInjectionWorld?.GetOrCreateSystemManaged<P_UninstallSystem>();
                uninstallSystem.UninstallPlatter();
            }
        }

        [SettingsUISection(AboutGroup)]
        public string Version => PlatterMod.Version;

        [SettingsUISection(AboutGroup)]
        public string InformationalVersion => PlatterMod.InformationalVersion;

        [SettingsUISection(AboutGroup)]
        public string Credits => Credit;

        [SettingsUISection(AboutGroup)]
        public bool Github {
            set {
                try {
                    Application.OpenURL($"https://github.com/lucarager/CS2-Platter");
                } catch (Exception e) {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutGroup)]
        public bool Discord {
            set {
                try {
                    Application.OpenURL($"https://discord.gg/QFxmPa2wCa");
                } catch (Exception e) {
                    UnityEngine.Debug.LogException(e);
                }
            }
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
