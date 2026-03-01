namespace NetworkTools.Settings {
    #region Using Statements

    using System;
    using Colossal.IO.AssetDatabase;
    using Game.Input;
    using Game.Modding;
    using Game.Settings;
    using UnityEngine;

    #endregion

    /// <summary>
    ///     The mod's settings.
    /// </summary>
    [FileLocation(NetworkToolsMod.ModName)]
    [SettingsUIGroupOrder(KeybindingsGroupStr, AboutGroupStr)]
    [SettingsUIShowGroupName(KeybindingsGroupStr, AboutGroupStr)]
    public class NT_Settings : ModSetting {
        private const string CreditStr = "Made with <3 by Luca.";
        public const string KeybindingsGroupStr = "KeybindingsGroupStr";
        public const string AboutGroupStr = "AboutGroupStr";
        public const string ToggleToolPanelStr = nameof(ToggleToolPanel);

        /// <summary>
        ///     Initializes a new instance of the <see cref="NT_Settings" /> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod" /> instance.</param>
        public NT_Settings(IMod mod)
            : base(mod) {
        }


        [SettingsUIKeyboardBinding(BindingKeyboard.T, ToggleToolPanelStr, ctrl: true)]
        public ProxyBinding ToggleToolPanel { get; set; }

        [SettingsUISection(AboutGroupStr)]
        public bool Discord
        {
            set
            {
                try {
                    Application.OpenURL("https://discord.gg/QFxmPa2wCa");
                }
                catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutGroupStr)]
        public bool Github
        {
            set
            {
                try {
                    Application.OpenURL("https://github.com/lucarager/CS2-NetworkTools");
                }
                catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutGroupStr)] public string Credits => CreditStr;

        [SettingsUISection(AboutGroupStr)] public string InformationalVersion => NetworkToolsMod.InformationalVersion;

        [SettingsUISection(AboutGroupStr)] public string Version => NetworkToolsMod.Version;

        /// <summary>
        ///     Restores mod settings to default.
        /// </summary>
        public override void SetDefaults() {
        }
    }
}