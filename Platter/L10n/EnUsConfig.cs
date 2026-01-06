// <copyright file="EnUsConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.L10n {
    #region Using Statements

    using System.Collections.Generic;
    using Colossal;
    using PS = Platter.Settings.PlatterModSettings;

    #endregion
        
    /// <summary>
    /// Configures the English (US) localization for Platter Mod.
    /// </summary>
    public class EnUsConfig : IDictionarySource {
        private readonly Dictionary<string, string> m_Localization;
        private readonly PS                         m_Setting;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnUsConfig"/> class.
        /// </summary>
        /// <param name="setting">PS</param>
        public EnUsConfig(PS setting) {
            m_Setting = setting;

            m_Localization = new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), PlatterMod.Id },
                { m_Setting.GetBindingMapLocaleID(), PlatterMod.Id },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(PS.GeneralTab), "General" },
                { m_Setting.GetOptionTabLocaleID(PS.KeybindingsTab), "Keybindings" },
                { m_Setting.GetOptionTabLocaleID(PS.AdvancedTab), "Advanced" },

                // Groups
                { m_Setting.GetOptionGroupLocaleID(PS.KeybindingsGroup), "Keyboard" },
                { m_Setting.GetOptionGroupLocaleID(PS.MousebindingsGroup), "Mouse" },
                { m_Setting.GetOptionGroupLocaleID(PS.UninstallGroup), "Uninstall" },
                { m_Setting.GetOptionGroupLocaleID(PS.AboutGroup), "About" },
                { m_Setting.GetOptionGroupLocaleID(PS.GeneralGroup), "General" },
                { m_Setting.GetOptionGroupLocaleID(PS.AdvancedGroup), "Advanced" },

                // Overlay for Tools
                { m_Setting.GetOptionLabelLocaleID(PS.EnableOverlayForToolsName), "Auto-Enable Parcel Overlay for key tools" },
                { m_Setting.GetOptionDescLocaleID(PS.EnableOverlayForToolsName), "When enabled, parcel overlays will show when using key vanilla tools like Net Tool etc. \n\r Disable this to control the overlay with the \"Toggle Overlay\" option manually." },
                
                // Uninstaller
                { m_Setting.GetOptionLabelLocaleID(PS.RemoveParcelsName), "Delete all Parcels" },
                { m_Setting.GetOptionDescLocaleID(PS.RemoveParcelsName), "Removes all parcels from the map, permanently. Buildings that are not on a vanilla zone grid will despawn, unless you have a mod that prevents that." },
                { m_Setting.GetOptionWarningLocaleID(PS.RemoveParcelsName), "WARNING: Permanently remove all parcels from this save?" },

                // Remove Icons
                { m_Setting.GetOptionLabelLocaleID(nameof(PS.RemoveIcons)), "Remove Orphaned Icons" },
                { m_Setting.GetOptionDescLocaleID(nameof(PS.RemoveIcons)), "Removes orphaned icon entities from the map that may have been left behind." },

                // ToggleRenderActionName
                { m_Setting.GetBindingKeyLocaleID(PS.ToggleRenderName), "Toggle \"Parcel Overlay\"" },
                { m_Setting.GetOptionLabelLocaleID(PS.ToggleRenderName), "Toggle \"Parcel Overlay\"" },
                { m_Setting.GetOptionDescLocaleID(PS.ToggleRenderName), "Shortcut to toggle the rendering of parcel overlays on or off." },

                // Block Width Scrollers
                { m_Setting.GetOptionLabelLocaleID(PS.DepthScrollName), "Increase/Decrease Parcel depth" },
                { m_Setting.GetBindingKeyLocaleID(PS.DepthScrollName), "Increase/Decrease Parcel depth" },
                { m_Setting.GetOptionDescLocaleID(PS.DepthScrollName), "Shortcut to increase/decrease a Parcel's depth" },
                { "Common.ACTION[Platter.Platter.PlatterMod/BlockDepthAction]", "Increase/Decrease Parcel depth" },

                // Block Depth Scrollers
                { m_Setting.GetOptionLabelLocaleID(PS.WidthScrollName), "Increase/Decrease Parcel width" },
                { m_Setting.GetBindingKeyLocaleID(PS.WidthScrollName), "Increase/Decrease Parcel width" },
                { m_Setting.GetOptionDescLocaleID(PS.WidthScrollName), "Shortcut to increase/decrease a Parcel's depth" },
                { "Common.ACTION[Platter.Platter.PlatterMod/BlockWidthAction]", "Increase/Decrease Parcel width" },

                // Setback scroller
                { m_Setting.GetOptionLabelLocaleID(PS.SetbackScrollName), "Increase/Decrease road setback" },
                { m_Setting.GetBindingKeyLocaleID(PS.SetbackScrollName), "Increase/Decrease road setback" },
                { m_Setting.GetOptionDescLocaleID(PS.SetbackScrollName), "Shortcut to increase/decrease a Parcel's setback from the road." },
                { "Common.ACTION[Platter.Platter.PlatterMod/SetbackAction]", "Increase/Decrease setback" },

                // ToggleSpawnActionName
                { m_Setting.GetBindingKeyLocaleID(PS.ToggleSpawnName), "Toggle \"Allow Spawning on Parcels\"" },
                { m_Setting.GetOptionLabelLocaleID(PS.ToggleSpawnName), "Toggle \"Allow Spawning on Parcels\"" },
                { m_Setting.GetOptionDescLocaleID(PS.ToggleSpawnName), "Shortcut to toggle allowing the spawning of buildings on parcels." },

                // OpenPlatterPanelActionName
                { m_Setting.GetBindingKeyLocaleID(PS.OpenPanelName), "Open Platter Panel" },
                { m_Setting.GetOptionLabelLocaleID(PS.OpenPanelName), "Open Platter Panel" },
                { m_Setting.GetOptionDescLocaleID(PS.OpenPanelName), "Shortcut to open the Platter panel." },

                // About
                { m_Setting.GetOptionLabelLocaleID(nameof(PS.Version)), "Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PS.InformationalVersion)), "Informational Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PS.Credits)), string.Empty },
                { m_Setting.GetOptionLabelLocaleID(nameof(PS.Github)), "GitHub" },
                { m_Setting.GetOptionDescLocaleID(nameof(PS.Github)), "Opens a browser window to https://github.com/lucarager/CS2-Platter" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PS.Discord)), "Discord" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PS.ResetTutorial)), "Reset Tutorial" },
                { m_Setting.GetOptionDescLocaleID(nameof(PS.ResetTutorial)), "Reset Tutorial" },
                { m_Setting.GetOptionDescLocaleID(nameof(PS.Discord)), "Opens link to join the CS:2 Modding Discord" },

                // UI
                { "PlatterMod.UI.Button.Infopanel.InspectParcel", "Inspect connected Parcel" },
                { "PlatterMod.UI.Button.Infopanel.InspectBuilding", "Inspect connected Building" },
                { "PlatterMod.UI.Button.Infopanel.InspectRoad", "Inspect connected Road" },
                { "PlatterMod.UI.SectionTitle.ToolMode", "Tool Mode" },
                { "PlatterMod.UI.SectionTitle.ViewLayers", "View Layers" },
                { "PlatterMod.UI.SectionTitle.Prezoning", "Pre-Zone" },
                { "PlatterMod.UI.SectionTitle.ParcelControls", "Parcel Controls" },
                { "PlatterMod.UI.SectionTitle.RenderParcels", "Always show Parcel Overlay" },
                { "PlatterMod.UI.SectionTitle.AllowSpawn", "Allow spawning buildings on Parcels" },
                { "PlatterMod.UI.SectionTitle.SnapMode", "Snapping" },
                { "PlatterMod.UI.SectionTitle.SnapSpacing", "Road Setback" },
                { "PlatterMod.UI.SectionTitle.ParcelWidth", "Parcel Width" },
                { "PlatterMod.UI.SectionTitle.ParcelDepth", "Parcel Depth" },
                { "PlatterMod.UI.SectionTitle.Search", "Search" },
                { "PlatterMod.UI.SectionTitle.Category", "Category" },
                { "PlatterMod.UI.SectionTitle.AssetPacks", "Asset Packs" },
                { "PlatterMod.UI.SectionTitle.Changelog", "View Changelog" },
                { "PlatterMod.UI.Label.ParcelSizeUnit", "cells" },
                { "PlatterMod.UI.Label.NewUpdatesAvailable", "(New updates)" },
                { "PlatterMod.UI.Label.NoZoneMatchesFilter", "No zone matches filter" },
                { "PlatterMod.UI.Button.CreateParcelFromZone", "Create Parcel with this Zone" },
                { "PlatterMod.UI.Tooltip.Infopanel.InspectParcel", "Inspect Parcel" },
                { "PlatterMod.UI.Tooltip.Infopanel.InspectBuilding", "Inspect Building" },
                { "PlatterMod.UI.Tooltip.Infopanel.InspectRoad", "Inspect Road" },
                { "PlatterMod.UI.Tooltip.PlopMode", "Plop Mode" },
                { "PlatterMod.UI.Tooltip.RoadPlatMode", "Road Platting Mode is development. Stay tuned!" },
                { "PlatterMod.UI.Tooltip.SnapModeNone", "No Snapping" },
                { "PlatterMod.UI.Tooltip.SnapModeZoneSide", "Snap to sides of the vanilla zone grid" },
                { "PlatterMod.UI.Tooltip.SnapModeRoadSide", "Snap to sides of a road" },
                { "PlatterMod.UI.Tooltip.SnapModeParcelEdge", "Snap to parcels" },
                { "PlatterMod.UI.Tooltip.SnapModeParcelFrontAlign", "Snap to parcel corners" },
                { "PlatterMod.UI.Tooltip.SnapModeAll", "Enable all snap modes" },
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

                // Building/Zone Tooltips
                {
                    "PlatterMod.UI.Tooltip.ZoneBuildingCount.Front",
                    "{COUNT} front access {X}x{Y} buildings in selected zone."
                },
                {
                    "PlatterMod.UI.Tooltip.ZoneBuildingCount.FrontCorner",
                    "{COUNT1} front and {COUNT2} corner access {X}x{Y} buildings in selected zone."
                },
                {
                    "PlatterMod.UI.Tooltip.ZoneBuildingCount.None",
                    "No {X}x{Y} buildings in selected zone. Smaller buildings may spawn on parcel."
                },
                
                {
                    "PlatterMod.UI.Tooltip.BuildingCount",// Deprecated
                    "[Front] {COUNT} available {X}x{Y} front access buildings in the selected zone."
                },
                {
                    "PlatterMod.UI.Tooltip.BuildingCountWarning",// Deprecated
                    "[Front] No {X}x{Y} front access buildings in selected zone. Smaller buildings may spawn on this parcel."
                },
                { 
                    "PlatterMod.UI.Tooltip.BuildingCornerCount",// Deprecated
                    "[Corner] {COUNT} available {X}x{Y} corner access buildings in the selected zone."
                },
                {
                    "PlatterMod.UI.Tooltip.BuildingCornerCountWarning",// Deprecated
                    "[Corner] No {X}x{Y} corner access buildings in selected zone. Smaller buildings may spawn on this parcel."
                },

                // FirstLaunch Modal
                { "PlatterMod.UI.Modals.FirstLaunch.Title", "Thanks for installing Platter!" },
                { "PlatterMod.UI.Modals.FirstLaunch.Subtitle", "Here's a quick intro to get you started" },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial1.Title", "Platter adds \"Parcels\" to the game" },
                {
                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial1.Text",
                    "Access parcels via the __Platter tab__ in the zone toolbar __(CTRL+P)__.\r\nPick a parcel and start plopping!\r\n\r\nNo need to manually block the vanilla grid — you can place parcels right on top."
                },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial2.Title", "Parcel Options" },
                {
                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial2.Text",
                    "Select __Pre-Zone__ to automatically zone parcels and get information about your zone/size combination. Adjust __Road Setback__, __Depth__, and __Width__ using scrollwheel shortcuts or the tool options panel.\r\nParcels also fully support the vanilla zoning tools."
                },
                { "PlatterMod.UI.Modals.FirstLaunch.Tutorial3.Title", "Advanced Uses" },
                {
                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial3.Text",
                    "You can freely manipulate parcels with __MoveIt__ and __modify roads without affecting your zoning__.\r\n\r\nUse the __top-left Platter Menu__ to toggle global options, like the parcel overlay or buildings spawning on parcels."
                },
                { "PlatterMod.UI.Modals.FirstLaunch.Disclaimer.Title", "Beta Disclaimer" },
                {
                    "PlatterMod.UI.Modals.FirstLaunch.Disclaimer.Text",
                    "Platter is an experimental beta mod.\r\n\r\nUse the Remove Parcels button on Platter's Settings page to safely remove custom parcels from your save before removing the mod."
                },
                { "PlatterMod.UI.Modals.FirstLaunch.Button", "Get plattin'!" },

                // Parcel Prefabs & Category Prefabs
                { "Assets.NAME[PlatterCat]", "Platter - Parcels" },
                { "Assets.DESCRIPTION[PlatterCat]", "Ploppable parcels with zone blocks." },
                { "SubServices.NAME[PlatterCat]", "Platter - Parcels" },
                { "Assets.SUB_SERVICE_DESCRIPTION[PlatterCat]", "Ploppable parcels with zone blocks." },
                { "Assets.NAME[PlatterUnzoned]", "Unzoned" },
            };

            var lotSizes = Platter.Systems.P_PrefabsCreateSystem.AvailableParcelLotSizes;
            for (var width = lotSizes.x; width <= lotSizes.z; width++) {
                for (var depth = lotSizes.y; depth <= lotSizes.w; depth++) {
                    m_Localization[$"Assets.NAME[Parcel {width}x{depth}]"] = $"Parcel ({width}x{depth})";
                    m_Localization[$"Assets.DESCRIPTION[Parcel {width}x{depth}]"] = $"A Parcel with zone blocks. {width} cells wide, {depth} cells deep";
                }
            }
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