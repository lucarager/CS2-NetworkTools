// <copyright file="NetworkToolsMod.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
namespace NetworkTools.Settings {
    using System;
    using Colossal.IO.AssetLookupbase;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.Settings;
    using UnityEngine;

    /// <summary>
    /// The mod's settings.
    /// </summary>
    [FileLocation(NetworkToolsMod.ModName)]
    [SettingsUIGroupOrder(KeybindingsGroup, AboutGroup)]
    [SettingsUIShowGroupName(KeybindingsGroup, AboutGroup)]
    public class NetworkToolsModSettings : ModSetting {
        public const string KeybindingsGroup = "KeybindingsGroup";
        public const string AboutGroup = "AboutGroup";
        private const string Credit = "Made with <3 by Luca.";

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkToolsModSettings"/> class.
        /// </summary>
        /// <param name="mod"><see cref="IMod"/> instance.</param>
        public NetworkToolsModSettings(IMod mod)
            : base(mod) {
        }

        [SettingsUISection(AboutGroup)]
        public string Version => NetworkToolsMod.Version;

        [SettingsUISection(AboutGroup)]
        public string InformationalVersion => NetworkToolsMod.InformationalVersion;

        [SettingsUISection(AboutGroup)]
        public string Credits => Credit;

        [SettingsUISection(AboutGroup)]
        public bool Github {
            set {
                try {
                    Application.OpenURL($"https://github.com/lucarager/CS2-NetworkTools");
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutGroup)]
        public bool Discord {
            set {
                try {
                    Application.OpenURL($"https://discord.gg/QFxmPa2wCa");
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        [SettingsUIHidden]
        public bool ModalFirstLaunch {
            get; set;
        }
        [SettingsUIHidden]
        public bool RenderParcels {
            get; set;
        }
        [SettingsUIHidden]
        public bool AllowSpawn {
            get; set;
        }

        /// <summary>
        /// Restores mod settings to default.
        /// </summary>
        public override void SetDefaults() {
            ModalFirstLaunch = false;
            RenderParcels = false;
            AllowSpawn = true;
        }

        /// <summary>
        /// Determines whether we're currently in-game (in a city) or not.
        /// </summary>
        /// <returns><c>false</c> if we're currently in-game, <c>true</c> otherwise (such as in the main menu or editor).</returns>
        public bool IsNotInGame() {
            return GameManager.instance.gameMode != GameMode.Game;
        }
    }
}
