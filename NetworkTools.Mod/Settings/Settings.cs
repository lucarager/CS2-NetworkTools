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
    [SettingsUIKeyboardAction(ToggleToolPanelStr, ActionType.Button, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(OpenTool1Str, ActionType.Button, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(OpenTool2Str, ActionType.Button, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(OpenTool3Str, ActionType.Button, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(OpenTool4Str, ActionType.Button, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(OpenTool5Str, ActionType.Button, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(OpenTool6Str, ActionType.Button, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(OpenTool7Str, ActionType.Button, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(OpenTool8Str, ActionType.Button, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(OpenTool9Str, ActionType.Button, Usages.kToolUsage)]
    public class NT_Settings : ModSetting {
        private const string CreditStr = "Made with <3 by Luca.";
        public const string KeybindingsGroupStr = "KeybindingsGroupStr";
        public const string AboutGroupStr = "AboutGroupStr";
        public const string ToggleToolPanelStr = nameof(ToggleToolPanel);
        public const string OpenTool1Str = nameof(OpenTool1);
        public const string OpenTool2Str = nameof(OpenTool2);
        public const string OpenTool3Str = nameof(OpenTool3);
        public const string OpenTool4Str = nameof(OpenTool4);
        public const string OpenTool5Str = nameof(OpenTool5);
        public const string OpenTool6Str = nameof(OpenTool6);
        public const string OpenTool7Str = nameof(OpenTool7);
        public const string OpenTool8Str = nameof(OpenTool8);
        public const string OpenTool9Str = nameof(OpenTool9);

        /// <summary>
        ///     Initializes a new instance of the <see cref="NT_Settings" /> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod" /> instance.</param>
        public NT_Settings(IMod mod)
            : base(mod) {
        }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.T, ToggleToolPanelStr, ctrl: true)]
        public ProxyBinding ToggleToolPanel { get; set; }


        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit1, OpenTool1Str, shift: true)]
        public ProxyBinding OpenTool1 { get; set; }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit2, OpenTool2Str, shift: true)]
        public ProxyBinding OpenTool2 { get; set; }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit3, OpenTool3Str, shift: true)]
        public ProxyBinding OpenTool3 { get; set; }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit4, OpenTool4Str, shift: true)]
        public ProxyBinding OpenTool4 { get; set; }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit5, OpenTool5Str, shift: true)]
        public ProxyBinding OpenTool5 { get; set; }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit6, OpenTool6Str, shift: true)]
        public ProxyBinding OpenTool6 { get; set; }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit7, OpenTool7Str, shift: true)]
        public ProxyBinding OpenTool7 { get; set; }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit8, OpenTool8Str, shift: true)]
        public ProxyBinding OpenTool8 { get; set; }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit9, OpenTool9Str, shift: true)]
        public ProxyBinding OpenTool9 { get; set; }


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