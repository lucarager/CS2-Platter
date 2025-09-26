// <copyright file="I18nDictionary.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Settings {
    using Colossal;
    using Colossal.IO.AssetDatabase.Internal;
    using Game.Input;
    using Game.Tools;
    using System.Collections.Generic;


    /// <summary>
    /// Todo.
    /// </summary>
    public class I18nDictionary : IDictionarySource {
        private readonly PlatterModSettings m_Setting;
        private readonly Dictionary<string, string> m_Localization;

        /// <summary>
        /// Initializes a new instance of the <see cref="I18nDictionary"/> class.
        /// </summary>
        /// <param name="setting">PlatterModSettings.</param>
        public I18nDictionary(PlatterModSettings setting) {
            m_Setting = setting;

            m_Localization = new Dictionary<string, string>() {
                { m_Setting.GetSettingsLocaleID(), PlatterMod.Id },
                // ToggleRenderActionName
                { m_Setting.GetBindingKeyLocaleID(nameof(PlatterModSettings.ToggleRenderActionName)), "Binding Key: ToggleRenderActionName" },
                { m_Setting.GetBindingKeyHintLocaleID(nameof(PlatterModSettings.ToggleRenderActionName)), "Hint Tooltip: ToggleRenderActionName" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterToggleRender)), "Label: ToggleRenderActionName" },
                { m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterToggleRender)), "Description: ToggleRenderActionName" },
                // ToggleSpawnActionName
                { m_Setting.GetBindingKeyLocaleID(nameof(PlatterModSettings.ToggleSpawnActionName)), "Binding Key: ToggleSpawnActionName" },
                { m_Setting.GetBindingKeyHintLocaleID(nameof(PlatterModSettings.ToggleSpawnActionName)), "Hint Tooltip: ToggleSpawnActionName" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterToggleSpawn)), "Label: ToggleSpawnActionName" },
                { m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterToggleSpawn)), "Description: ToggleSpawnActionName" },
                // IncreaseParcelWidthActionName
                { m_Setting.GetBindingKeyLocaleID(nameof(PlatterModSettings.IncreaseParcelWidthActionName)), "Binding Key: IncreaseParcelWidthActionName" },
                { m_Setting.GetBindingKeyHintLocaleID(nameof(PlatterModSettings.IncreaseParcelWidthActionName)), "Hint Tooltip: IncreaseParcelWidthActionName" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterIncreaseParcelWidth)), "Label: IncreaseParcelWidthActionName" },
                { m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterIncreaseParcelWidth)), "Description: IncreaseParcelWidthActionName" },
                // DecreaseParcelWidthActionName
                { m_Setting.GetBindingKeyLocaleID(nameof(PlatterModSettings.DecreaseParcelWidthActionName)), "Binding Key: DecreaseParcelWidthActionName" },
                { m_Setting.GetBindingKeyHintLocaleID(nameof(PlatterModSettings.DecreaseParcelWidthActionName)), "Hint Tooltip: DecreaseParcelWidthActionName" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterDecreaseParcelWidth)), "Label: DecreaseParcelWidthActionName" },
                { m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterDecreaseParcelWidth)), "Description: DecreaseParcelWidthActionName" },
                // IncreaseParcelDepthActionName
                { m_Setting.GetBindingKeyLocaleID(nameof(PlatterModSettings.IncreaseParcelDepthActionName)), "Binding Key: IncreaseParcelDepthActionName" },
                { m_Setting.GetBindingKeyHintLocaleID(nameof(PlatterModSettings.IncreaseParcelDepthActionName)), "Hint Tooltip: IncreaseParcelDepthActionName" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterIncreaseParcelDepth)), "Label: IncreaseParcelDepthActionName" },
                { m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterIncreaseParcelDepth)), "Description: IncreaseParcelDepthActionName" },
                // DecreaseParcelDepthActionName
                { m_Setting.GetBindingKeyLocaleID(nameof(PlatterModSettings.DecreaseParcelDepthActionName)), "Binding Key: DecreaseParcelDepthActionName" },
                { m_Setting.GetBindingKeyHintLocaleID(nameof(PlatterModSettings.DecreaseParcelDepthActionName)), "Hint Tooltip: DecreaseParcelDepthActionName" },
                { m_Setting.GetOptionLabelLocaleID(nameof(PlatterModSettings.PlatterDecreaseParcelDepth)), "Label: DecreaseParcelDepthActionName" },
                { m_Setting.GetOptionDescLocaleID(nameof(PlatterModSettings.PlatterDecreaseParcelDepth)), "Description: DecreaseParcelDepthActionName" },
                // Parcel Prefabs & Category Prefabs
                { "Assets.NAME[PlatterCat]", "Platter Category" },
                { "Assets.DESCRIPTION[PlatterCat]", "Platter Category Description" },
                { "SubServices.NAME[PlatterCat]", "Platter Category 2" },
                { "Assets.SUB_SERVICE_DESCRIPTION[PlatterCat]", "Platter Category 2" },
                { "Assets.NAME[Parcel 2x2]", "Parcel 2x2" },
                { "Assets.DESCRIPTION[Parcel 2x2]", "Parcel 2x2" },
                { "Assets.NAME[Parcel 2x3]", "Parcel 2x3" },
                { "Assets.DESCRIPTION[Parcel 2x3]", "Parcel 2x3" },
                { "Assets.NAME[Parcel 2x4]", "Parcel 2x4" },
                { "Assets.DESCRIPTION[Parcel 2x4]", "Parcel 2x4" },
                { "Assets.NAME[Parcel 2x5]", "Parcel 2x5" },
                { "Assets.DESCRIPTION[Parcel 2x5]", "Parcel 2x5" },
                { "Assets.NAME[Parcel 2x6]", "Parcel 2x6" },
                { "Assets.DESCRIPTION[Parcel 2x6]", "Parcel 2x6" },
                { "Assets.NAME[Parcel 3x2]", "Parcel 3x2" },
                { "Assets.DESCRIPTION[Parcel 3x2]", "Parcel 3x2" },
                { "Assets.NAME[Parcel 3x3]", "Parcel 3x3" },
                { "Assets.DESCRIPTION[Parcel 3x3]", "Parcel 3x3" },
                { "Assets.NAME[Parcel 3x4]", "Parcel 3x4" },
                { "Assets.DESCRIPTION[Parcel 3x4]", "Parcel 3x4" },
                { "Assets.NAME[Parcel 3x5]", "Parcel 3x5" },
                { "Assets.DESCRIPTION[Parcel 3x5]", "Parcel 3x5" },
                { "Assets.NAME[Parcel 3x6]", "Parcel 3x6" },
                { "Assets.DESCRIPTION[Parcel 3x6]", "Parcel 3x6" },
                { "Assets.NAME[Parcel 4x2]", "Parcel 4x2" },
                { "Assets.DESCRIPTION[Parcel 4x2]", "Parcel 4x2" },
                { "Assets.NAME[Parcel 4x3]", "Parcel 4x3" },
                { "Assets.DESCRIPTION[Parcel 4x3]", "Parcel 4x3" },
                { "Assets.NAME[Parcel 4x4]", "Parcel 4x4" },
                { "Assets.DESCRIPTION[Parcel 4x4]", "Parcel 4x4" },
                { "Assets.NAME[Parcel 4x5]", "Parcel 4x5" },
                { "Assets.DESCRIPTION[Parcel 4x5]", "Parcel 4x5" },
                { "Assets.NAME[Parcel 4x6]", "Parcel 4x6" },
                { "Assets.DESCRIPTION[Parcel 4x6]", "Parcel 4x6" },
                { "Assets.NAME[Parcel 5x2]", "Parcel 5x2" },
                { "Assets.DESCRIPTION[Parcel 5x2]", "Parcel 5x2" },
                { "Assets.NAME[Parcel 5x3]", "Parcel 5x3" },
                { "Assets.DESCRIPTION[Parcel 5x3]", "Parcel 5x3" },
                { "Assets.NAME[Parcel 5x4]", "Parcel 5x4" },
                { "Assets.DESCRIPTION[Parcel 5x4]", "Parcel 5x4" },
                { "Assets.NAME[Parcel 5x5]", "Parcel 5x5" },
                { "Assets.DESCRIPTION[Parcel 5x5]", "Parcel 5x5" },
                { "Assets.NAME[Parcel 5x6]", "Parcel 5x6" },
                { "Assets.DESCRIPTION[Parcel 5x6]", "Parcel 5x6" },
                { "Assets.NAME[Parcel 6x2]", "Parcel 6x2" },
                { "Assets.DESCRIPTION[Parcel 6x2]", "Parcel 6x2" },
                { "Assets.NAME[Parcel 6x3]", "Parcel 6x3" },
                { "Assets.DESCRIPTION[Parcel 6x3]", "Parcel 6x3" },
                { "Assets.NAME[Parcel 6x4]", "Parcel 6x4" },
                { "Assets.DESCRIPTION[Parcel 6x4]", "Parcel 6x4" },
                { "Assets.NAME[Parcel 6x5]", "Parcel 6x5" },
                { "Assets.DESCRIPTION[Parcel 6x5]", "Parcel 6x5" },
                { "Assets.NAME[Parcel 6x6]", "Parcel 6x6" },
                { "Assets.DESCRIPTION[Parcel 6x6]", "Parcel 6x6" },
                // UI
                { "PlatterMod.UI.SectionTitle.Prezoning", "Pre-Zone" },
                { "PlatterMod.UI.SectionTitle.Lotsize", "Lot Size" },
                { "PlatterMod.UI.SectionTitle.ParcelControls", "Parcel Controls" },
                { "PlatterMod.UI.SectionTitle.RenderParcels", "Render Parcels" },
                { "PlatterMod.UI.SectionTitle.AllowSpawn", "Enable Spawning on Parcels" },
            };
        }

        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts) {
            return m_Localization;
        }

        /// <inheritdoc/>
        public void Unload() {
        }
    }
}
