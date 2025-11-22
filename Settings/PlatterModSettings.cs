// <copyright file="PlatterModSettings.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Settings {
    #region Using Statements

    using System;
    using Colossal.IO.AssetDatabase;
    using Game;
    using Game.Input;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.Settings;
    using Systems;
    using Unity.Entities;
    using UnityEngine;

    #endregion

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation("ModsSettings/" + nameof(PlatterMod) + "/" + nameof(PlatterMod))]
    [SettingsUIKeyboardAction(IncreaseParcelWidthActionName, ActionType.Button, "Platter")]
    [SettingsUIKeyboardAction(DecreaseParcelWidthActionName, ActionType.Button, "Platter")]
    [SettingsUIKeyboardAction(IncreaseParcelDepthActionName, ActionType.Button, "Platter")]
    [SettingsUIKeyboardAction(DecreaseParcelDepthActionName, ActionType.Button, "Platter")]
    [SettingsUIGroupOrder(KeybindingsGroup, UninstallGroup, AboutGroup)]
    [SettingsUIShowGroupName(KeybindingsGroup, UninstallGroup, AboutGroup)]
    public class PlatterModSettings : ModSetting {
        public const  string AboutGroup                    = "AboutGroup";
        public const  string ApplyActionName               = "PlatterToolApply";
        public const  string CancelActionName              = "PlatterToolCancel";
        private const string Credit                        = "Made with <3 by Luca.";
        public const  string DecreaseParcelDepthActionName = "DecreaseParcelDepthActionName";
        public const  string DecreaseParcelWidthActionName = "DecreaseParcelWidthActionName";
        public const  string IncreaseParcelDepthActionName = "IncreaseParcelDepthActionName";
        public const  string IncreaseParcelWidthActionName = "IncreaseParcelWidthActionName";
        public const  string KeybindingsGroup              = "KeybindingsGroup";
        public const  string ToggleRenderActionName        = "ToggleRenderActionName";
        public const  string ToggleSpawnActionName         = "ToggleSpawnActionName";
        public const  string UninstallGroup                = "UninstallGroup";

        [SettingsUIHidden]
        public bool AllowSpawn { get; set; } = true;

        [SettingsUISection(AboutGroup)]
        public bool Discord {
            set {
                try {
                    Application.OpenURL("https://discord.gg/QFxmPa2wCa");
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutGroup)]
        public bool Github {
            set {
                try {
                    Application.OpenURL("https://github.com/lucarager/CS2-Platter");
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUIHidden]
        public bool Modals_FirstLaunchTutorial { get; set; }

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

        [SettingsUIHidden]
        public bool RenderParcels { get; set; }

        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.X, DecreaseParcelDepthActionName, true)]
        public ProxyBinding PlatterDecreaseParcelDepth { get; set; }

        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.X, DecreaseParcelWidthActionName, ctrl: true)]
        public ProxyBinding PlatterDecreaseParcelWidth { get; set; }

        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Z, IncreaseParcelDepthActionName, true)]
        public ProxyBinding PlatterIncreaseParcelDepth { get; set; }

        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Z, IncreaseParcelWidthActionName, ctrl: true)]
        public ProxyBinding PlatterIncreaseParcelWidth { get; set; }

        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.P, ToggleRenderActionName, ctrl: true)]
        public ProxyBinding PlatterToggleRender { get; set; }

        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.L, ToggleSpawnActionName, ctrl: true)]
        public ProxyBinding PlatterToggleSpawn { get; set; }

        /// <summary>
        /// Gets or sets the Platter Tool apply action (copied from game action).
        /// </summary>
        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIHidden]
        [SettingsUIBindingMimic(InputManager.kToolMap, "Apply")]
        public ProxyBinding PlatterToolApply { get; set; }

        /// <summary>
        /// Gets or sets the Platter Tool cancel action (copied from game action).
        /// </summary>
        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIHidden]
        [SettingsUIBindingMimic(InputManager.kShortcutsMap, "Cancel")]
        public ProxyBinding PlatterToolCancel { get; set; }

        [SettingsUISection(AboutGroup)]
        public string Credits => Credit;

        [SettingsUISection(AboutGroup)]
        public string InformationalVersion => PlatterMod.InformationalVersion;

        [SettingsUISection(AboutGroup)]
        public string Version => PlatterMod.Version;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatterModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public PlatterModSettings(IMod mod)
            : base(mod) { }

        /// <summary>
        /// Restores mod settings to default.
        /// </summary>
        public override void SetDefaults() {
            Modals_FirstLaunchTutorial = false;
            RenderParcels              = false;
            AllowSpawn                 = true;
        }

        /// <summary>
        /// Determines whether we're currently in-game (in a city) or not.
        /// </summary>
        /// <returns><c>false</c> if we're currently in-game, <c>true</c> otherwise (such as in the main menu or editor).</returns>
        public bool IsNotInGame() { return GameManager.instance.gameMode != GameMode.Game; }
    }
}