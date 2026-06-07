namespace NetworkTools.Settings {
    using System;
    using Colossal.IO.AssetDatabase;
    using Game.Input;
    using Game.Modding;
    using Game.Settings;
    using Game.UI.Widgets;
    using NetworkTools.Systems.Tools;
    using UnityEngine;

    /// <summary>
    ///     The mod's settings.
    /// </summary>
    [FileLocation("NetworkTools")]
    [SettingsUIGroupOrder(GeneralGroupStr, KeybindingsGroupStr, DebugGroupStr, AboutGroupStr)]
    [SettingsUIShowGroupName(GeneralGroupStr, KeybindingsGroupStr, DebugGroupStr, AboutGroupStr)]
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
    [SettingsUIKeyboardAction(ApplyTransformationStr, ActionType.Button, Usages.kToolUsage)]
    public class NT_Settings : ModSetting {
        private const string CreditStr = "Made with <3 by Luca.";
        public const string GeneralGroupStr = "GeneralGroupStr";
        public const string KeybindingsGroupStr = "KeybindingsGroupStr";
        public const string AboutGroupStr = "AboutGroupStr";
        public const string DebugGroupStr = "DebugGroupStr";
        public const string DistanceUnitMeters = "Meters";
        public const string DistanceUnitUnits = "Units";
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
        public const string ApplyTransformationStr = nameof(ApplyTransformation);

        /// <summary>
        ///     Initializes a new instance of the <see cref="NT_Settings" /> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod" /> instance.</param>
        public NT_Settings(IMod mod)
            : base(mod) {
        }

        [SettingsUISection(GeneralGroupStr)]
        [SettingsUIDropdown(typeof(NT_Settings), nameof(GetDistanceUnitItems))]
        public string DistanceUnit { get; set; } = DistanceUnitMeters;

        public DropdownItem<string>[] GetDistanceUnitItems() {
            return new DropdownItem<string>[] {
                new() {
                    value = DistanceUnitMeters,
                    displayName = GetOptionLabelLocaleID(DistanceUnitMeters),
                },
                new() {
                    value = DistanceUnitUnits,
                    displayName = GetOptionLabelLocaleID(DistanceUnitUnits),
                },
            };
        }

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.T, ToggleToolPanelStr, shift: true)]
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

        [SettingsUISection(KeybindingsGroupStr)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Enter, ApplyTransformationStr)]
        public ProxyBinding ApplyTransformation { get; set; }


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

        [SettingsUISection(AboutGroupStr)] public string InformationalVersion => NetworkToolsMod.Instance.InformationalVersion;

        [SettingsUISection(AboutGroupStr)] public string Version => NetworkToolsMod.Instance.Version;

        /// <summary>
        ///     Enables debug mode on the active tool system.
        /// </summary>
        [SettingsUISection(DebugGroupStr)]
#if !IS_DEBUG
        [SettingsUIHidden]
#endif
        public bool DebugMode { get; set; }

        /// <summary>
        ///     Persisted snap selection flags (hidden from UI).
        /// </summary>
        [SettingsUIHidden]
        public int SavedSelectedSnaps { get; set; } = (int)SnapOption.All;

        /// <summary>
        ///     Persisted target selection flags (hidden from UI).
        /// </summary>
        [SettingsUIHidden]
        public int SavedSelectedTargets { get; set; } = (int)TargetOption.All;

        /// <summary>
        ///     Persisted view selection flags (hidden from UI).
        /// </summary>
        [SettingsUIHidden]
        public int SavedSelectedViews { get; set; } = (int)ViewOption.None;

        /// <summary>
        ///     Persisted anarchy toggle state (hidden from UI).
        /// </summary>
        [SettingsUIHidden]
        public bool SavedAnarchyEnabled { get; set; }

        /// <summary>
        ///     Persisted tool parameter values (hidden from UI).
        ///     JSON-encoded dictionary keyed by "toolID.paramKey".
        /// </summary>
        [SettingsUIHidden]
        public string SavedParameterValues { get; set; } = "{}";

        /// <summary>
        ///     Restores mod settings to default.
        /// </summary>
        public override void SetDefaults() {
            DistanceUnit = DistanceUnitMeters;
            DebugMode = false;
            SavedSelectedSnaps   = (int)SnapOption.All;
            SavedSelectedTargets = (int)TargetOption.All;
            SavedSelectedViews   = (int)ViewOption.None;
            SavedAnarchyEnabled  = false;
            SavedParameterValues = "{}";
        }
    }
}
