// <copyright file="EnUsConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Settings {
    #region Using Statements

    using System.Collections.Generic;
    using Colossal;

    #endregion

    /// <summary>
    /// Configures the English (US) localization for NetworkTools Mod.
    /// </summary>
    public class EnUsConfig : IDictionarySource {
        private readonly Dictionary<string, string> m_Localization;
        private readonly NT_Settings    m_Setting;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnUsConfig"/> class.
        /// </summary>
        /// <param name="setting">NetworkToolsModSettings.</param>
        public EnUsConfig(NT_Settings setting) {
            m_Setting = setting;

            m_Localization = new Dictionary<string, string> {
                { m_Setting.GetSettingsLocaleID(), NetworkToolsMod.Id },

                // Sections

                // Groups
                { m_Setting.GetOptionGroupLocaleID(nameof(NT_Settings.KeybindingsGroup)), "Key Bindings" },
                { m_Setting.GetOptionGroupLocaleID(nameof(NT_Settings.AboutGroup)), "About" },

                // About
                { m_Setting.GetOptionLabelLocaleID(nameof(NT_Settings.Version)), "Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(NT_Settings.InformationalVersion)), "Informational Version" },
                { m_Setting.GetOptionLabelLocaleID(nameof(NT_Settings.Credits)), string.Empty },
                { m_Setting.GetOptionLabelLocaleID(nameof(NT_Settings.Github)), "GitHub" }, {
                    m_Setting.GetOptionDescLocaleID(nameof(NT_Settings.Github)),
                    "Opens a browser window to https://github.com/lucarager/CS2-NetworkTools"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(NT_Settings.Discord)), "Discord" },
                { m_Setting.GetOptionDescLocaleID(nameof(NT_Settings.Discord)), "Opens link to join the CS:2 Modding Discord" },
            };
        }

        /// <inheritdoc/>
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts) {
            return m_Localization;
        }

        /// <inheritdoc/>
        public void Unload() { }
    }
}