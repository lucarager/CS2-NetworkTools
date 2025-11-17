// <copyright file="I18nConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Settings {
    using System.Collections.Generic;
    using System.Security.Policy;
    using Colossal;
    using Game.Tools;

    /// <summary>
    /// Configures the English (US) localization for NetworkTools Mod.
    /// </summary>
    public class EnUsConfig : IDictionarySource {
        private readonly NetworkToolsModSettings m_Setting;
        private readonly Dictionary<string, string> m_Localization;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnUsConfig"/> class.
        /// </summary>
        /// <param name="setting">NetworkToolsModSettings.</param>
        public EnUsConfig(NetworkToolsModSettings setting) {
            m_Setting = setting;

            m_Localization = new Dictionary<string, string>() {
                { m_Setting.GetSettingsLocaleID(), NetworkToolsMod.Id },

                // Sections

                // Groups
                { m_Setting.GetOptionGroupLocaleID(nameof(NetworkToolsModSettings.KeybindingsGroup)), "Key Bindings" },
                { m_Setting.GetOptionGroupLocaleID(nameof(NetworkToolsModSettings.AboutGroup)), "About" },
                
                // About
                { m_Setting.GetOptionLabelLocaleID(nameof(NetworkToolsModSettings.Version)), "Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(NetworkToolsModSettings.InformationalVersion)), "Informational Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(NetworkToolsModSettings.Credits)), string.Empty },
                { m_Setting.GetOptionLabelLocaleID(nameof(NetworkToolsModSettings.Github)), "GitHub" },
                { m_Setting.GetOptionDescLocaleID(nameof(NetworkToolsModSettings.Github)), "Opens a browser window to https://github.com/lucarager/CS2-NetworkTools" },
                { m_Setting.GetOptionLabelLocaleID(nameof(NetworkToolsModSettings.Discord)), "Discord" },
                { m_Setting.GetOptionDescLocaleID(nameof(NetworkToolsModSettings.Discord)), "Opens link to join the CS:2 Modding Discord" },
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
