// <copyright file="I18nDictionary.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Settings {
    using Colossal;
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
