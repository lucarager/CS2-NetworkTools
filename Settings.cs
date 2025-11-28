// <copyright file="Settings.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Settings {
    #region Using Statements

    using System;
    using Colossal.IO.AssetDatabase;
    using Game;
    using Game.Input;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.Settings;
    using UnityEngine;

    #endregion

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(NetworkToolsMod.ModName)]
    [SettingsUIGroupOrder(KeybindingsGroup, AboutGroup)]
    [SettingsUIShowGroupName(KeybindingsGroup, AboutGroup)]
    public class NetworkToolsModSettings : ModSetting {
        public const  string AboutGroup               = "AboutGroup";
        private const string Credit                   = "Made with <3 by Luca.";
        public const  string KeybindingsGroup         = "KeybindingsGroup";
        public const  string ApplyActionName          = "ApplyActionName";
        public const  string SecondaryApplyActionName = "SecondaryApplyActionName";

        [SettingsUIMouseBinding(BindingMouse.Left, ApplyActionName)]
        public ProxyBinding ApplyMimic { get; set; }

        [SettingsUIMouseBinding(BindingMouse.Right, SecondaryApplyActionName)]
        public ProxyBinding SecondaryApplyMimic { get; set; }

        [SettingsUIHidden]
        public bool AllowSpawn { get; set; }

        [SettingsUISection(AboutGroup)]
        public bool Discord {
            set {
                try {
                    Application.OpenURL("https://discord.gg/QFxmPa2wCa");
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutGroup)]
        public bool Github {
            set {
                try {
                    Application.OpenURL("https://github.com/lucarager/CS2-NetworkTools");
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUIHidden]
        public bool ModalFirstLaunch { get; set; }

        [SettingsUIHidden]
        public bool RenderParcels { get; set; }

        [SettingsUISection(AboutGroup)]
        public string Credits => Credit;

        [SettingsUISection(AboutGroup)]
        public string InformationalVersion => NetworkToolsMod.InformationalVersion;

        [SettingsUISection(AboutGroup)]
        public string Version => NetworkToolsMod.Version;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkToolsModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public NetworkToolsModSettings(IMod mod)
            : base(mod) { }

        /// <summary>
        /// Restores mod settings to default.
        /// </summary>
        public override void SetDefaults() {
            ModalFirstLaunch = false;
            RenderParcels    = false;
            AllowSpawn       = true;
        }

        /// <summary>
        /// Determines whether we're currently in-game (in a city) or not.
        /// </summary>
        /// <returns><c>false</c> if we're currently in-game, <c>true</c> otherwise (such as in the main menu or editor).</returns>
        public bool IsNotInGame() { return GameManager.instance.gameMode != GameMode.Game; }
    }
}