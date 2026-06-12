// <copyright file="NetworkToolsMod.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools {
    using Colossal;       // IDictionarySource
    using Game;           // UpdateSystem, SystemUpdatePhase
    using Game.Modding;   // IMod, ModSetting

    using LucaModsCommon.Mod;
    using LucaModsCommon.Rendering;

    using NetworkTools.L10n;
    using NetworkTools.Settings;
    using NetworkTools.Systems;
    using NetworkTools.Systems.Rendering;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Connect;
    using NetworkTools.Systems.Tools.Parallel;
    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Systems.Tooltips;
    using NetworkTools.Systems.UI;

    using NT_GenerateToolSystem = NetworkTools.Systems.Tools.Generate.NT_GenerateToolSystem;
    using NT_UITooltipSystem = NetworkTools.Systems.Tooltips.NT_UITooltipSystem;

    /// <summary>
    /// Mod entry point. Shared lifecycle (logging, settings, localization, Harmony, tests, asset
    /// host registration) is provided by <see cref="LucaModBase{TSelf}"/>.
    /// </summary>
    public sealed class NetworkToolsMod : LucaModBase<NetworkToolsMod>, IMod {
        /// <inheritdoc/>
        public override string ModName => "NetworkTools";

        /// <inheritdoc/>
        public override string Id => "NetworkTools";

        /// <inheritdoc/>
        protected override string UiHostPrefix => "nt";

        /// <summary>
        /// Gets the mod's settings, typed as <see cref="NT_Settings"/>.
        /// </summary>
        public new NT_Settings Settings => (NT_Settings)base.Settings;

        /// <inheritdoc/>
        protected override ModSetting CreateSettings(IMod mod) => new NT_Settings(mod);

        /// <inheritdoc/>
        protected override IDictionarySource CreateEnUsLocalization(ModSetting settings) =>
            new EnUsConfig((NT_Settings)settings);

        /// <inheritdoc/>
        protected override void RegisterSystems(UpdateSystem updateSystem) {
            // Core systems
            updateSystem.UpdateAt<NT_PrefabsCreateSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<NT_PostProcessingSystem>(SystemUpdatePhase.Modification4); // Search system updates on Mod5, run before then
            // Tools
            updateSystem.UpdateAt<NT_RoadShapeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<NT_AddNodeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<NT_RemoveNodeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<NT_SuperNodeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<NT_SlideNodeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<NT_ConnectToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<NT_ParallelToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<NT_GenerateToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<NT_PrefabCacheToolSystem>(SystemUpdatePhase.ToolUpdate);
            // UI
            updateSystem.UpdateAt<NT_UISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<NT_PrefabSelectionUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<NT_UITooltipSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<NT_ActionTooltipSystem>(SystemUpdatePhase.UITooltip);
            // Rendering
            updateSystem.UpdateAt<NT_OverlaySystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<NT_ToolOverlayRenderSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<CustomOverlayRenderSystem>(SystemUpdatePhase.Rendering);
        }

        /// <summary>
        /// Exports the en-US dictionary to L10N/lang/en-US.json on the I18N build configuration so
        /// translators have an up-to-date source file.
        /// </summary>
        protected override void GenerateLanguageFile() => ExportEnUsLocalization();
    }
}
