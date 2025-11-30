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
    [SettingsUIGroupOrder(KeybindingsGroup, UninstallGroup, AboutGroup)]
    [SettingsUIShowGroupName(KeybindingsGroup, UninstallGroup, AboutGroup)]
    public class PlatterModSettings : ModSetting {
        // Groups
        public const string AboutGroup        = "AboutGroup";
        public const string KeybindingsGroup  = "KeybindingsGroup";
        public const string UninstallGroup    = "UninstallGroup";
        // Actions
        public const string OpenPanelName     = nameof(PlatterOpenPanel);
        public const string ToggleRenderName  = nameof(PlatterToggleRender);
        public const string ToggleSpawnName   = nameof(PlatterToggleSpawn);
        public const string DepthScrollName   = nameof(PlatterDepthScrollAction);
        public const string WidthScrollName   = nameof(PlatterWidthScrollAction);
        public const string SetbackScrollName = nameof(PlatterSetbackScrollAction);
        public const string RemoveParcelsName = nameof(RemoveParcels);
        // Statics
        private const string Credit                    = "Made with <3 by Luca.";

        [SettingsUIHidden]
        public bool AllowSpawn { get; set; } = true;

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

        // Fake binding to show in settings UI
        [SettingsUISection(KeybindingsGroup)]
        public string PlatterDepthScrollAction => "Ctrl + ScrollWheel";

        // Fake binding to show in settings UI
        [SettingsUISection(KeybindingsGroup)]
        public string PlatterWidthScrollAction => "Alt + ScrollWheel";

        // Fake binding to show in settings UI
        [SettingsUISection(KeybindingsGroup)]
        public string PlatterSetbackScrollAction => "Ctrl + Shift + ScrollWheel";

        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.P, OpenPanelName, ctrl: true)]
        public ProxyBinding PlatterOpenPanel { get; set; }

        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.P, ToggleRenderName, ctrl: true, shift: true)]
        public ProxyBinding PlatterToggleRender { get; set; }

        [SettingsUISection(KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.P, ToggleSpawnName, ctrl: true, shift: true, alt: true)]
        public ProxyBinding PlatterToggleSpawn { get; set; }

        [SettingsUISection(AboutGroup)]
        public string InformationalVersion => PlatterMod.InformationalVersion;

        [SettingsUISection(AboutGroup)]
        public string Version => PlatterMod.Version;

        [SettingsUISection(AboutGroup)]
        public string Credits => Credit;

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

        [SettingsUISection(AboutGroup)]
        [SettingsUIButton]
        public bool ResetTutorial {
            set => Modals_FirstLaunchTutorial = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatterModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public PlatterModSettings(IMod mod)
            : base(mod) { }

        public bool AlwaysDisabled() { return true; }

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