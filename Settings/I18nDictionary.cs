namespace Platter.Settings {
    using Colossal;
    using System.Collections.Generic;

    public class I18nDictionary : IDictionarySource {
        private readonly PlatterModSettings m_Setting;
        private Dictionary<string, string> m_Localization;

        public I18nDictionary(PlatterModSettings setting) {
            m_Setting = setting;

            m_Localization = new Dictionary<string, string>() {
                { m_Setting.GetSettingsLocaleID(), PlatterMod.Id },
                { "Assets.NAME[Quay01]", "Quay" },
                { "Assets.DESCRIPTION[Quay01]", "Network raised above terrain with one or two walls along one or both sides. Can be applied to left or right side of applicable networks." },
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
