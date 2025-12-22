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
    using static Game.Prefabs.CompositionFlags;

    #endregion

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation("ModsSettings/" + nameof(PlatterMod) + "/" + nameof(PlatterMod))]
    [SettingsUITabOrder(GeneralTab, KeybindingsTab, AdvancedTab)]
    [SettingsUIGroupOrder(GeneralGroup, KeybindingsGroup, MousebindingsGroup, UninstallGroup, AboutGroup)]
    [SettingsUIShowGroupName(GeneralGroup, KeybindingsGroup, MousebindingsGroup, UninstallGroup, AboutGroup)]
    public class PlatterModSettings : ModSetting {
        #region Strings

        // Tabs
        public const string GeneralTab     = "GeneralTab";
        public const string KeybindingsTab = "KeybindingsTab";
        public const string AdvancedTab    = "AdvancedTab";
        // Groups
        public const string GeneralGroup       = "GeneralGroup";
        public const string AboutGroup         = "AboutGroup";
        public const string KeybindingsGroup   = "KeybindingsGroup";
        public const string MousebindingsGroup = "MousebindingsGroup";
        public const string UninstallGroup     = "UninstallGroup";
        public const string AdvancedGroup      = "AdvancedGroup";
        // Actions
        public const string OpenPanelName             = nameof(PlatterOpenPanel);
        public const string ToggleRenderName          = nameof(PlatterToggleRender);
        public const string ToggleSpawnName           = nameof(PlatterToggleSpawn);
        public const string DepthScrollName           = nameof(PlatterDepthScrollAction);
        public const string WidthScrollName           = nameof(PlatterWidthScrollAction);
        public const string SetbackScrollName         = nameof(PlatterSetbackScrollAction);
        public const string RemoveParcelsName         = nameof(RemoveParcels);
        public const string EnableOverlayForToolsName = nameof(EnableOverlayForTools);
        // Statics
        private const string Credit = "Made with <3 by Luca.";

        #endregion

        #region [General]

        [SettingsUISection(GeneralTab, GeneralGroup)]
        public bool EnableOverlayForTools { get; set; } = true;

        [SettingsUISection(GeneralTab, AboutGroup)]
        public string InformationalVersion => PlatterMod.InformationalVersion;

        [SettingsUISection(GeneralTab, AboutGroup)]
        public string Version => PlatterMod.Version;

        [SettingsUISection(GeneralTab, AboutGroup)]
        public string Credits => Credit;

        [SettingsUISection(GeneralTab, AboutGroup)]
        public bool Discord {
            set {
                try {
                    Application.OpenURL("https://discord.gg/QFxmPa2wCa");
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(GeneralTab, AboutGroup)]
        public bool Github {
            set {
                try {
                    Application.OpenURL("https://github.com/lucarager/CS2-Platter");
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(GeneralTab, AboutGroup)]
        [SettingsUIButton]
        public bool ResetTutorial {
            set => Modals_FirstLaunchTutorial = false;
        }

        [SettingsUISection(GeneralTab, UninstallGroup)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUIDisableByCondition(typeof(PlatterModSettings), nameof(IsNotInGame))]
        public bool RemoveParcels {
            set {
                var uninstallSystem = World.DefaultGameObjectInjectionWorld?.GetOrCreateSystemManaged<P_UninstallSystem>();
                uninstallSystem.UninstallPlatter();
            }
        }

#if IS_DEBUG
        [SettingsUISection(GeneralTab, AboutGroup)]
        [SettingsUIButton]
        public bool ResetChangelog {
            set => LastViewedChangelogVersion = 0;
        }
#endif

        #endregion

        #region [Keybindings]

        [SettingsUISection(KeybindingsTab, KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.P, OpenPanelName, ctrl: true)]
        public ProxyBinding PlatterOpenPanel { get; set; }

        [SettingsUISection(KeybindingsTab, KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.P, ToggleRenderName, ctrl: true, shift: true)]
        public ProxyBinding PlatterToggleRender { get; set; }

        [SettingsUISection(KeybindingsTab, KeybindingsGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.P, ToggleSpawnName, ctrl: true, shift: true, alt: true)]
        public ProxyBinding PlatterToggleSpawn { get; set; }

        // Fake binding to show in settings UI
        [SettingsUISection(KeybindingsTab, MousebindingsGroup)]
        public string PlatterDepthScrollAction => "Ctrl + ScrollWheel";

        // Fake binding to show in settings UI
        [SettingsUISection(KeybindingsTab, MousebindingsGroup)]
        public string PlatterWidthScrollAction => "Alt + ScrollWheel";

        // Fake binding to show in settings UI
        [SettingsUISection(KeybindingsTab, MousebindingsGroup)]
        public string PlatterSetbackScrollAction => "Ctrl + Shift + ScrollWheel";

        #endregion


        #region Advanced

        [SettingsUISection(AdvancedTab, AdvancedGroup)]
        [SettingsUIButton]
        public bool RemoveIcons {
            set {
                var uninstallSystem = World.DefaultGameObjectInjectionWorld?.GetOrCreateSystemManaged<P_UninstallSystem>();
                uninstallSystem.RemoveIcons();
            }
        }

        #endregion

        #region Internal/Hidden

        [SettingsUIHidden]
        public const uint CurrentChangelogVersion = 15;

        [SettingsUIHidden]
        public bool AllowSpawn { get; set; } = true;

        [SettingsUIHidden]
        public bool Modals_FirstLaunchTutorial { get; set; }

        [SettingsUIHidden]
        public uint LastViewedChangelogVersion { get; set; }

        [SettingsUIHidden]
        public bool RenderParcels { get; set; }

        #endregion

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
            Modals_FirstLaunchTutorial   = false;
            RenderParcels                = false;
            AllowSpawn                   = true;
            LastViewedChangelogVersion   = 0;
            EnableOverlayForTools = true;
        }

        /// <summary>
        /// Determines whether we're currently in-game (in a city) or not.
        /// </summary>
        /// <returns><c>false</c> if we're currently in-game, <c>true</c> otherwise (such as in the main menu or editor).</returns>
        public bool IsNotInGame() { return GameManager.instance.gameMode != GameMode.Game; }
    }
}