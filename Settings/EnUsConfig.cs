// <copyright file="EnUsConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Settings {
    using System.Collections.Generic;
    using System.Security.Policy;
    using Colossal;
    using Game.Tools;
    using Platter;

    /// <summary>
    /// Configures the English (US) localization for Platter Mod.
    /// </summary>
    public class EnUsConfig : IDictionarySource {
        private readonly Dictionary<string, string> m_Localization;
        private readonly PlatterModSettings         m_Setting;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnUsConfig"/> class.
        /// </summary>
        /// <param name="setting">PlatterModSettings.</param>
        public EnUsConfig(PlatterModSettings setting) {
            m_Setting = setting;

            m_Localization = new Dictionary<string, string> {
                { m_Setting.GetSettingsLocaleID(), PlatterMod.Id },

                // Sections

                // Groups
                { m_Setting.GetOptionGroupLocaleID(nameof(PlatterModSettings.KeybindingsGroup)), "Key Bindings" },
                { m_Setting.GetOptionGroupLocaleID(nameof(PlatterModSettings.UninstallGroup)), "Uninstall" },
                { m_Setting.GetOptionGroupLocaleID(nameof(PlatterModSettings.AboutGroup)), "About" },

                // Uninstaller
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.RemoveParcels)), "Delete all Parcels" }, {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.RemoveParcels)),
                    "Removes all parcels from the map, permanently. Buildings that are not on a vanilla zone grid will despawn, unless you have a mod that prevents that."
                }, {
                    m_Setting.GetOptionWarningLocaleID(nameof(PlatterModSettings.RemoveParcels)),
                    "WARNING: Permanently remove all parcels from this save?"
                },

                // ToggleRenderActionName
                { m_Setting.GetBindingKeyLocaleID(PlatterModSettings.ToggleRenderActionName), "Toggle \"Parcel Overlay\"" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterToggleRender)), "Toggle \"Parcel Overlay\"" }, {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterToggleRender)),
                    "Shortcut to toggle the rendering of parcel overlays on or off."
                },

                // ToggleSpawnActionName
                {
                    m_Setting.GetBindingKeyLocaleID(PlatterModSettings.ToggleSpawnActionName),
                    "Toggle \"Allow Spawning on Parcels\""
                }, {
                    m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterToggleSpawn)),
                    "Toggle \"Allow Spawning on Parcels\""
                }, {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterToggleSpawn)),
                    "Shortcut to toggle allowing the spawning of buildings on parcels."
                },

                // IncreaseParcelWidthActionName
                { m_Setting.GetBindingKeyLocaleID(PlatterModSettings.IncreaseParcelWidthActionName), "Increase Parcel width" }, {
                    m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterIncreaseParcelWidth)),
                    "Increase Parcel width"
                }, {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterIncreaseParcelWidth)),
                    "Shortcut to increase Parcel width while plopping Parcels"
                }, {
                    m_Setting.GetBindingKeyHintLocaleID(PlatterModSettings.IncreaseParcelWidthActionName), "Increase Parcel width"
                },

                // DecreaseParcelWidthActionName
                { m_Setting.GetBindingKeyLocaleID(PlatterModSettings.DecreaseParcelWidthActionName), "Decrease Parcel width" }, {
                    m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterDecreaseParcelWidth)),
                    "Decrease Parcel width"
                }, {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterDecreaseParcelWidth)),
                    "Shortcut to decrease Parcel width while plopping Parcels"
                }, {
                    m_Setting.GetBindingKeyHintLocaleID(PlatterModSettings.DecreaseParcelWidthActionName), "Decrease Parcel width"
                },

                // IncreaseParcelDepthActionName
                { m_Setting.GetBindingKeyLocaleID(PlatterModSettings.IncreaseParcelDepthActionName), "Increase Parcel depth" }, {
                    m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterIncreaseParcelDepth)),
                    "Increase Parcel depth"
                }, {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterIncreaseParcelDepth)),
                    "Shortcut to increase Parcel depth while plopping Parcels"
                }, {
                    m_Setting.GetBindingKeyHintLocaleID(PlatterModSettings.IncreaseParcelDepthActionName), "Increase Parcel depth"
                },

                // DecreaseParcelDepthActionName
                { m_Setting.GetBindingKeyLocaleID(PlatterModSettings.DecreaseParcelDepthActionName), "Decrease Parcel depth" }, {
                    m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterDecreaseParcelDepth)),
                    "Decrease Parcel depth"
                }, {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterDecreaseParcelDepth)),
                    "Shortcut to decrease Parcel depth while plopping Parcels"
                }, {
                    m_Setting.GetBindingKeyHintLocaleID(PlatterModSettings.DecreaseParcelDepthActionName), "Decrease Parcel depth"
                },

                // About
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.Version)), "Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.InformationalVersion)), "Informational Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.Credits)), string.Empty },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.Github)), "GitHub" }, {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.Github)),
                    "Opens a browser window to https://github.com/lucarager/CS2-Platter"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.Discord)), "Discord" }, {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.Discord)),
                    "Opens link to join the CS:2 Modding Discord"
                },

                // UI
                { "PlatterMod.UI.SectionTitle.Prezoning", "Pre-Zone" },
                { "PlatterMod.UI.SectionTitle.ParcelControls", "Parcel Controls" },
                { "PlatterMod.UI.SectionTitle.RenderParcels", "Enable Parcels Overlay" },
                { "PlatterMod.UI.SectionTitle.AllowSpawn", "Allow spawning buildings on Parcels" },
                { "PlatterMod.UI.SectionTitle.SnapMode", "Snapping" },
                { "PlatterMod.UI.SectionTitle.SnapSpacing", "Road Setback" },
                { "PlatterMod.UI.SectionTitle.ParcelWidth", "Parcel Width" },
                { "PlatterMod.UI.SectionTitle.ParcelDepth", "Parcel Depth" },
                { "PlatterMod.UI.Label.ParcelSizeUnit", "cells" },
                { "PlatterMod.UI.Button.CreateParcelFromZone", "Create Parcel with this Zone" },
                { "PlatterMod.UI.Tooltip.SnapModeNone", "No Snapping" },
                { "PlatterMod.UI.Tooltip.SnapModeZoneSide", "Snap to sides of a road" },
                { "PlatterMod.UI.Tooltip.SnapModeRoadSide", "Snap to sides of the vanilla zone grid" },
                { "PlatterMod.UI.Tooltip.SnapSpacingIncrease", "Increase setback" },
                { "PlatterMod.UI.Tooltip.SnapSpacingDecrease", "Decrease setback" },
                { "PlatterMod.UI.Tooltip.SnapSpacingAmount", "Setback between road and parcel" },
                { "PlatterMod.UI.Tooltip.BlockWidthIncrease", "Increase parcel width" },
                { "PlatterMod.UI.Tooltip.BlockWidthDecrease", "Decrease parcel width" },
                { "PlatterMod.UI.Tooltip.BlockWidthNumber", "Parcel with" },
                { "PlatterMod.UI.Tooltip.BlockDepthIncrease", "Increase parcel depth" },
                { "PlatterMod.UI.Tooltip.BlockDepthDecrease", "Decrease parcel depth" },
                { "PlatterMod.UI.Tooltip.BlockDepthNumber", "Parcel depth" },
                { "PlatterMod.UI.Tooltip.ToggleRenderParcels", "Toggle Parcels Overlay" },
                { "PlatterMod.UI.Tooltip.ToggleAllowSpawn", "Toggle allowing buildings to spawn on Parcels" },
                { "PlatterMod.UI.Tooltip.BuildingCount", "{COUNT} available {X}x{Y} buildings in the selected zone." }, {
                    "PlatterMod.UI.Tooltip.BuildingCountWarning",
                    "No {X}x{Y} buildings in selected zone. Smaller buildings may spawn on this parcel."
                },

                // FirstLaunch Modal
                { "PlatterMod.UI.Modals.FirstLaunch.Title", "Thanks for installing Platter!" },
                { "PlatterMod.UI.Modals.FirstLaunch.Subtitle", "Here's a quick intro to get you started" },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial1.Title", "Platter adds \"Parcels\" to the game" },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial1.Text", "You can find ploppable parcels in __the new Platter tab__ in the zone toolbar.\r\n Oh, and no need to block or remove vanilla blocks, plop parcels right on top!" },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial2.Title", "Parcels work just like vanilla blocks." },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial2.Text", "You can use the tools familiar to you to zone and grow buildings.\r\n __Use the Fill zone tool for best results__ - it will limit the flood area to a parcel." },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial3.Text", "The __top left Platter menu__ allows you to toggle the parcel overlay and temporarily block buildings growing on parcels." },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial4.Text", "__Advanced Line Tool__ and __MoveIt__ are great mods to use with Platter!" },
                { "PlatterMod.UI.Modals.FirstLaunch.Disclaimer.Title", "Disclaimer" }, {
                    "PlatterMod.UI.Modals.FirstLaunch.Disclaimer.Text",
                    "Platter is an experimental beta mod. Should you wish to uninstall it, the Settings page contains an uninstall button that will safely remove all custom parcels from your save."
                },
                { "PlatterMod.UI.Modals.FirstLaunch.Button", "Get plattin'!" },

                // Parcel Prefabs & Category Prefabs
                { "Assets.NAME[PlatterCat]", "Platter - Parcels" },
                { "Assets.DESCRIPTION[PlatterCat]", "Ploppable parcels with zone blocks." },
                { "SubServices.NAME[PlatterCat]", "Platter - Parcels" },
                { "Assets.SUB_SERVICE_DESCRIPTION[PlatterCat]", "Ploppable parcels with zone blocks." },
                { "Assets.NAME[Parcel 2x2]", "Parcel (2x2)" },
                { "Assets.DESCRIPTION[Parcel 2x2]", "A Parcel with zone blocks. 2 cells wide, 2 cells deep" },
                { "Assets.NAME[Parcel 2x3]", "Parcel (2x3)" },
                { "Assets.DESCRIPTION[Parcel 2x3]", "A Parcel with zone blocks. 2 cells wide, 3 cells deep" },
                { "Assets.NAME[Parcel 2x4]", "Parcel (2x4)" },
                { "Assets.DESCRIPTION[Parcel 2x4]", "A Parcel with zone blocks. 2 cells wide, 4 cells deep" },
                { "Assets.NAME[Parcel 2x5]", "Parcel (2x5)" },
                { "Assets.DESCRIPTION[Parcel 2x5]", "A Parcel with zone blocks. 2 cells wide, 5 cells deep" },
                { "Assets.NAME[Parcel 2x6]", "Parcel (2x6)" },
                { "Assets.DESCRIPTION[Parcel 2x6]", "A Parcel with zone blocks. 2 cells wide, 6 cells deep" },
                { "Assets.NAME[Parcel 3x2]", "Parcel (3x2)" },
                { "Assets.DESCRIPTION[Parcel 3x2]", "A Parcel with zone blocks. 3 cells wide, 2 cells deep" },
                { "Assets.NAME[Parcel 3x3]", "Parcel (3x3)" },
                { "Assets.DESCRIPTION[Parcel 3x3]", "A Parcel with zone blocks. 3 cells wide, 3 cells deep" },
                { "Assets.NAME[Parcel 3x4]", "Parcel (3x4)" },
                { "Assets.DESCRIPTION[Parcel 3x4]", "A Parcel with zone blocks. 3 cells wide, 4 cells deep" },
                { "Assets.NAME[Parcel 3x5]", "Parcel (3x5)" },
                { "Assets.DESCRIPTION[Parcel 3x5]", "A Parcel with zone blocks. 3 cells wide, 5 cells deep" },
                { "Assets.NAME[Parcel 3x6]", "Parcel (3x6)" },
                { "Assets.DESCRIPTION[Parcel 3x6]", "A Parcel with zone blocks. 3 cells wide, 6 cells deep" },
                { "Assets.NAME[Parcel 4x2]", "Parcel (4x2)" },
                { "Assets.DESCRIPTION[Parcel 4x2]", "A Parcel with zone blocks. 4 cells wide, 2 cells deep" },
                { "Assets.NAME[Parcel 4x3]", "Parcel (4x3)" },
                { "Assets.DESCRIPTION[Parcel 4x3]", "A Parcel with zone blocks. 4 cells wide, 3 cells deep" },
                { "Assets.NAME[Parcel 4x4]", "Parcel (4x4)" },
                { "Assets.DESCRIPTION[Parcel 4x4]", "A Parcel with zone blocks. 4 cells wide, 4 cells deep" },
                { "Assets.NAME[Parcel 4x5]", "Parcel (4x5)" },
                { "Assets.DESCRIPTION[Parcel 4x5]", "A Parcel with zone blocks. 4 cells wide, 5 cells deep" },
                { "Assets.NAME[Parcel 4x6]", "Parcel (4x6)" },
                { "Assets.DESCRIPTION[Parcel 4x6]", "A Parcel with zone blocks. 4 cells wide, 6 cells deep" },
                { "Assets.NAME[Parcel 5x2]", "Parcel (5x2)" },
                { "Assets.DESCRIPTION[Parcel 5x2]", "A Parcel with zone blocks. 5 cells wide, 2 cells deep" },
                { "Assets.NAME[Parcel 5x3]", "Parcel (5x3)" },
                { "Assets.DESCRIPTION[Parcel 5x3]", "A Parcel with zone blocks. 5 cells wide, 3 cells deep" },
                { "Assets.NAME[Parcel 5x4]", "Parcel (5x4)" },
                { "Assets.DESCRIPTION[Parcel 5x4]", "A Parcel with zone blocks. 5 cells wide, 4 cells deep" },
                { "Assets.NAME[Parcel 5x5]", "Parcel (5x5)" },
                { "Assets.DESCRIPTION[Parcel 5x5]", "A Parcel with zone blocks. 5 cells wide, 5 cells deep" },
                { "Assets.NAME[Parcel 5x6]", "Parcel (5x6)" },
                { "Assets.DESCRIPTION[Parcel 5x6]", "A Parcel with zone blocks. 5 cells wide, 6 cells deep" },
                { "Assets.NAME[Parcel 6x2]", "Parcel (6x2)" },
                { "Assets.DESCRIPTION[Parcel 6x2]", "A Parcel with zone blocks. 6 cells wide, 2 cells deep" },
                { "Assets.NAME[Parcel 6x3]", "Parcel (6x3)" },
                { "Assets.DESCRIPTION[Parcel 6x3]", "A Parcel with zone blocks. 6 cells wide, 3 cells deep" },
                { "Assets.NAME[Parcel 6x4]", "Parcel (6x4)" },
                { "Assets.DESCRIPTION[Parcel 6x4]", "A Parcel with zone blocks. 6 cells wide, 4 cells deep" },
                { "Assets.NAME[Parcel 6x5]", "Parcel (6x5)" },
                { "Assets.DESCRIPTION[Parcel 6x5]", "A Parcel with zone blocks. 6 cells wide, 5 cells deep" },
                { "Assets.NAME[Parcel 6x6]", "Parcel (6x6)" },
                { "Assets.DESCRIPTION[Parcel 6x6]", "A Parcel with zone blocks. 6 cells wide, 6 cells deep" },
            };
        }

        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
                                                                     Dictionary<string, int>      indexCounts) {
            return m_Localization;
        }

        /// <inheritdoc/>
        public void Unload() { }
    }
}