// <copyright file="EnUsConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Settings {
    #region Using Statements

    using System.Collections.Generic;
    using Colossal;

    #endregion

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

            m_Localization = new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), PlatterMod.Id },

                // Sections

                // Groups
                { m_Setting.GetOptionGroupLocaleID(PlatterModSettings.KeybindingsGroup), "Key Bindings" },
                { m_Setting.GetOptionGroupLocaleID(PlatterModSettings.UninstallGroup), "Uninstall" },
                { m_Setting.GetOptionGroupLocaleID(PlatterModSettings.AboutGroup), "About" },

                // Uninstaller
                { m_Setting.GetOptionLabelLocaleID(PlatterModSettings.RemoveParcelsName), "Delete all Parcels" },
                {
                    m_Setting.GetOptionDescLocaleID(PlatterModSettings.RemoveParcelsName),
                    "Removes all parcels from the map, permanently. Buildings that are not on a vanilla zone grid will despawn, unless you have a mod that prevents that."
                },
                {
                    m_Setting.GetOptionWarningLocaleID(PlatterModSettings.RemoveParcelsName),
                    "WARNING: Permanently remove all parcels from this save?"
                },

                // ToggleRenderActionName
                { m_Setting.GetBindingKeyLocaleID(PlatterModSettings.ToggleRenderName), "Toggle \"Parcel Overlay\"" },
                { m_Setting.GetOptionLabelLocaleID(PlatterModSettings.ToggleRenderName), "Toggle \"Parcel Overlay\"" },
                {
                    m_Setting.GetOptionDescLocaleID(PlatterModSettings.ToggleRenderName),
                    "Shortcut to toggle the rendering of parcel overlays on or off."
                },

                // Block Width Scrollers
                { m_Setting.GetOptionLabelLocaleID(PlatterModSettings.DepthScrollName), "Increase/Decrease Parcel Depth" },
                { m_Setting.GetBindingKeyLocaleID(PlatterModSettings.DepthScrollName), "Increase/Decrease Parcel Depth" },
                { m_Setting.GetOptionDescLocaleID(PlatterModSettings.DepthScrollName), "Shortcut to increase/decrease a Parcel's depth" },
                { "Common.ACTION[Platter/BlockDepthAction]", "Increase/Decrease Parcel Depth" },

                // Block Depth Scrollers
                { m_Setting.GetOptionLabelLocaleID(PlatterModSettings.WidthScrollName), "Increase/Decrease Parcel Width" },
                { m_Setting.GetBindingKeyLocaleID(PlatterModSettings.WidthScrollName), "Increase/Decrease Parcel Width" },
                { m_Setting.GetOptionDescLocaleID(PlatterModSettings.WidthScrollName), "Shortcut to increase/decrease a Parcel's depth" },
                { "Common.ACTION[Platter/BlockWidthAction]", "Increase/Decrease Parcel Width" },

                // Setback scroller
                { m_Setting.GetOptionLabelLocaleID(PlatterModSettings.SetbackScrollName), "Increase/Decrease Road setback" },
                { m_Setting.GetBindingKeyLocaleID(PlatterModSettings.SetbackScrollName), "Increase/Decrease Road setback" },
                { m_Setting.GetOptionDescLocaleID(PlatterModSettings.SetbackScrollName), "Shortcut to increase/decrease a Parcel's setback from the road." },
                { "Common.ACTION[Platter/SetbackAction]", "Increase/Decrease Setback" },

                // ToggleSpawnActionName
                {
                    m_Setting.GetBindingKeyLocaleID(PlatterModSettings.ToggleSpawnName),
                    "Toggle \"Allow Spawning on Parcels\""
                },
                {
                    m_Setting.GetOptionLabelLocaleID(PlatterModSettings.ToggleSpawnName),
                    "Toggle \"Allow Spawning on Parcels\""
                },
                {
                    m_Setting.GetOptionDescLocaleID(PlatterModSettings.ToggleSpawnName),
                    "Shortcut to toggle allowing the spawning of buildings on parcels."
                },

                // OpenPlatterPanelActionName
                {
                    m_Setting.GetBindingKeyLocaleID(PlatterModSettings.OpenPanelName),
                    "Open Platter Panel"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(PlatterModSettings.OpenPanelName),
                    "Open Platter Panel"
                },
                {
                    m_Setting.GetOptionDescLocaleID(PlatterModSettings.OpenPanelName),
                    "Shortcut to open the Platter panel."
                },

                // About
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.Version)), "Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.InformationalVersion)), "Informational Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.Credits)), string.Empty },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.Github)), "GitHub" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.Github)),
                    "Opens a browser window to https://github.com/lucarager/CS2-Platter"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.Discord)), "Discord" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.ResetTutorial)), "Reset Tutorial" },
                { m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.ResetTutorial)), "Reset Tutorial" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.Discord)),
                    "Opens link to join the CS:2 Modding Discord"
                },

                // UI
                { "PlatterMod.UI.SectionTitle.ToolMode", "Tool Mode" },
                { "PlatterMod.UI.SectionTitle.ViewLayers", "View Layers" },
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
                { "PlatterMod.UI.Tooltip.PlopMode", "Plop Mode" },
                { "PlatterMod.UI.Tooltip.RoadPlatMode", "Road Platting Mode is development. Stay tuned!" },
                { "PlatterMod.UI.Tooltip.SnapModeNone", "No Snapping" },
                { "PlatterMod.UI.Tooltip.SnapModeZoneSide", "Snap to sides of a road" },
                { "PlatterMod.UI.Tooltip.SnapModeRoadSide", "Snap to sides of the vanilla zone grid" },
                { "PlatterMod.UI.Tooltip.SnapSpacingIncrease", "Increase setback" },
                { "PlatterMod.UI.Tooltip.SnapSpacingDecrease", "Decrease setback" },
                { "PlatterMod.UI.Tooltip.SnapSpacingAmount", "Setback between road and parcel" },
                { "PlatterMod.UI.Tooltip.BlockWidthIncrease", "Increase parcel width" },
                { "PlatterMod.UI.Tooltip.BlockWidthDecrease", "Decrease parcel width" },
                { "PlatterMod.UI.Tooltip.BlockWidthNumber", "Parcel width" },
                { "PlatterMod.UI.Tooltip.BlockDepthIncrease", "Increase parcel depth" },
                { "PlatterMod.UI.Tooltip.BlockDepthDecrease", "Decrease parcel depth" },
                { "PlatterMod.UI.Tooltip.BlockDepthNumber", "Parcel depth" },
                { "PlatterMod.UI.Tooltip.ToggleRenderParcels", "Toggle Parcels Overlay" },
                { "PlatterMod.UI.Tooltip.ToggleAllowSpawn", "Toggle allowing buildings to spawn on Parcels" },
                { "PlatterMod.UI.Tooltip.ShowZonbes", "Toggle allowing buildings to spawn on Parcels" },
                { "PlatterMod.UI.Tooltip.ShowZones", "Show vanilla grid" },
                { "PlatterMod.UI.Tooltip.ShowContourLines", "Show countour lines" },
                { "PlatterMod.UI.Tooltip.BuildingCount", "{COUNT} available {X}x{Y} buildings in the selected zone." },
                {
                    "PlatterMod.UI.Tooltip.BuildingCountWarning",
                    "No {X}x{Y} buildings in selected zone. Smaller buildings may spawn on this parcel."
                },

                // FirstLaunch Modal
                { "PlatterMod.UI.Modals.FirstLaunch.Title", "Thanks for installing Platter!" },
                { "PlatterMod.UI.Modals.FirstLaunch.Subtitle", "Here's a quick intro to get you started" },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial1.Title", "Platter adds \"Parcels\" to the game" },
                {
                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial1.Text",
                    "Access parcels via the __Platter tab__ in the zone toolbar __(CTRL+P)__.\r\nSelect a parcel to start plopping.\r\n\r\nNo need to manually block the vanilla grid — you can place parcels directly on top."
                },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial2.Title", "Zoning Parcels" },
                {
                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial2.Text",
                    "Select __Pre-Zone__ to automatically zone parcels and get information about your zone/size combination.\r\n\r\nParcels also fully support the vanilla zoning tools."
                },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial2-2.Title", "Configuration" },
                {
                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial2-2.Text",
                    "Adjust __Road Setback__, __Depth__, and __Width__ using scrollwheel shortcuts or the tool options panel."
                },
                {
                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial3.Text",
                    "Use the __top-left menu__ to toggle the parcel overlay or temporarily halt buildings spawning on empty parcels."
                },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial4.Text", "__Advanced Line Tool__ and __MoveIt__ are great mods to use with Platter!" },
                { "PlatterMod.UI.Modals.FirstLaunch.Disclaimer.Title", "Beta Disclaimer" },
                {
                    "PlatterMod.UI.Modals.FirstLaunch.Disclaimer.Text",
                    "Platter is an experimental beta mod.\r\n\r\nTo uninstall: You must use the button in the Settings page to safely remove custom parcels from your save before removing the mod."
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