// <copyright file="Mod.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using Colossal;
    using Colossal.IO.AssetDatabase;
    using Colossal.Json;
    using Colossal.Localization;
    using Colossal.Logging;
    using Colossal.Reflection;
    using Colossal.TestFramework;
    using Colossal.UI;

    using Game;
    using Game.Modding;
    using Game.Rendering;
    using Game.SceneFlow;
    using Game.Serialization;
    using HarmonyLib;
    using NetworkTools.Components.Tools;
    using NetworkTools.L10N;
    using NetworkTools.Systems;
    using NetworkTools.Systems.Rendering;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Connect;
    using NetworkTools.Systems.Tools.Parallel;
    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Systems.Tooltips;
    using NetworkTools.Systems.UI;
    using NetworkTools.Utils;

    using Newtonsoft.Json;

    using Settings;

    using UnityEngine;
    using NT_UITooltipSystem = NetworkTools.Systems.Tooltips.NT_UITooltipSystem;
    using StreamReader = System.IO.StreamReader;

    /// <summary>
    /// Mod entry point.
    /// </summary>
    public class NetworkToolsMod : IMod {
        private static readonly string HarmonyPatchId = $"{nameof(NetworkTools)}.{nameof(NetworkToolsMod)}";

        /// <summary>
        /// The mod's default actionName.
        /// </summary>
        public const string ModName = "NetworkTools";

        /// <summary>
        /// An id used for bindings between UI and C#.
        /// </summary>
        public static readonly string Id = "NetworkTools";

        /// <summary>
        /// Harmony instance used for patching vanilla methods.
        /// </summary>
        private Harmony m_Harmony;

        /// <summary>
        /// Sets mod to test mode
        /// </summary>
        internal bool IsTestMode { get; set; } = false;

        /// <summary>
        /// Gets the mod's logger.
        /// </summary>
        internal ILog Log { get; private set; }

        /// <summary>
        /// Gets the instance reference.
        /// </summary>
        public static NetworkToolsMod Instance { get; private set; }

        /// <summary>
        /// Prefixed logger for module-level logging.
        /// </summary>
        private PrefixedLogger m_Log;

        /// <summary>
        /// Gets the mod's settings configuration.
        /// </summary>
        internal NT_Settings Settings { get; private set; }

        /// <summary>
        /// Gets the mod's informational version
        /// </summary>
        public static string InformationalVersion =>
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        /// <summary>
        /// Gets the mod's version
        /// </summary>
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString(4);

        /// <inheritdoc/>
        public void OnLoad(UpdateSystem updateSystem) {
            // Set instance reference.
            Instance = this;

            // Initialize logger.
            Log = LogManager
                  .GetLogger(ModName)
                  .SetShowsErrorsInUI(false);
#if IS_DEBUG
            Log = Log
                  .SetBacktraceEnabled(true)
                  .SetEffectiveness(Level.All);
#endif
            m_Log = new PrefixedLogger(nameof(NetworkToolsMod));
            m_Log.Info($"Loading {ModName} version {Assembly.GetExecutingAssembly().GetName().Version}");

            // Initialize Settings
            Settings = new NT_Settings(this);

            // Load i18n
            GameManager.instance.localizationManager.AddSource("en-US", new EnUsConfig(Settings));
            Log.Info("[NetworkTools] Loaded en-US.");
            LoadNonEnglishLocalizations();
            Log.Info("[NetworkTools] Loaded localization files.");

            // Generate i18n files
#if IS_DEBUG && EXPORT_EN_US
            GenerateLanguageFile();
#endif

            // Register mod settings to game options UI.
            Settings.RegisterInOptionsUI();

            // Load saved settings.
            AssetDatabase.global.LoadSettings("NetworkTools", Settings, new NT_Settings(this));

            // Apply input bindings.
            Settings.RegisterKeyBindings();

            // ## Activate Systems
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
            updateSystem.UpdateAt<NT_GridToolSystem>(SystemUpdatePhase.ToolUpdate);
            // UI
            updateSystem.UpdateAt<NT_UISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<NT_UITooltipSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<NT_HintTooltipSystem>(SystemUpdatePhase.UITooltip);
            // Rendering
            updateSystem.UpdateAt<NT_OverlaySystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<CustomOverlayRenderSystem>(SystemUpdatePhase.Rendering);
            // Debug Tools
            //updateSystem.UpdateBefore<NT_DebugSystem, ElectricityGraphSystem>(SystemUpdatePhase.Deserialize);

            // Harmony
            InitializeHarmonyPatches();

            // Add tests
            AddTests();

            // Add mod UI resource directory to UI resource handler.
            if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out var modAsset)) {
                Log.Error("Failed to get executable asset path. Exiting.");
                return;
            }

            var assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
            UIManager.defaultUISystem.AddHostLocation("nt", assemblyPath + "/Assets/");

            Log.Info($"Installed and enabled. RenderedFrame: {Time.renderedFrameCount}");
        }

        /// <inheritdoc/>
        public void OnDispose() {
            Log.Info("[NetworkTools] Disposing");
            Instance = null;

            // Clear settings menu entry.
            if (Settings != null) {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }

        /// <summary>
        /// Initializes Harmony patches for the assembly and logs all patched methods.
        /// </summary>
        private void InitializeHarmonyPatches() {
            m_Log.Debug("InitializeHarmonyPatches()");

            m_Harmony = new Harmony(HarmonyPatchId);
            m_Harmony.PatchAll(typeof(NetworkToolsMod).Assembly);
            var patchedMethods = m_Harmony.GetPatchedMethods().ToArray();

            foreach (var patchedMethod in patchedMethods) {
                m_Log.Debug($"InitializeHarmonyPatches() -- Patched method: {patchedMethod.Module.ScopeName}:{patchedMethod.Name}");
            }
        }

        private void GenerateLanguageFile() {
            m_Log.Debug("GenerateLanguageFile()");
            var localeDict = new EnUsConfig(Settings).ReadEntries(new List<IDictionaryEntryError>(), new Dictionary<string, int>())
                                                     .ToDictionary(pair => pair.Key, pair => pair.Value);
            var str = JsonConvert.SerializeObject(localeDict, Formatting.Indented);
            try {
                var path = GetThisFilePath();
                var directory = Path.GetDirectoryName(path);
                var exportPath = $@"{directory}/L10n/lang/en-US.json";
                File.WriteAllText(exportPath, str);
            } catch (Exception ex) {
                m_Log.Error(ex.ToString());
            }
        }

        /// <summary>
        /// Gets the file path of the calling source file using the CallerFilePath attribute.
        /// </summary>
        /// <param name="path">The path provided by the compiler via CallerFilePath attribute.</param>
        /// <returns>The file path of the calling code.</returns>
        private static string GetThisFilePath([CallerFilePath] string path = null) { return path; }

        private static void AddTests() {
            var log = LogManager.GetLogger(ModName);
            log.Debug("AddTests()");

            var m_ScenariosField = typeof(TestScenarioSystem).GetField("m_Scenarios", BindingFlags.Instance | BindingFlags.NonPublic);
            if (m_ScenariosField == null) {
                log.Error("AddTests() -- Could not find m_Scenarios");
                return;
            }

            var m_Scenarios = (Dictionary<string, TestScenarioSystem.Scenario>)m_ScenariosField.GetValue(TestScenarioSystem.instance);

            foreach (var type in GetTests()) {
                if (!type.IsClass || type.IsAbstract || type.IsInterface || !type.TryGetAttribute(
                        out TestDescriptorAttribute testDescriptorAttribute)) {
                    continue;
                }

                log.Debug($"AddTests() -- {testDescriptorAttribute.description}");

                m_Scenarios.Add(
                    testDescriptorAttribute.description,
                    new TestScenarioSystem.Scenario {
                        category  = testDescriptorAttribute.category,
                        testPhase = testDescriptorAttribute.testPhase,
                        test      = type,
                        disabled  = testDescriptorAttribute.disabled,
                    });
            }

            m_Scenarios = TestScenarioSystem.SortScenarios(m_Scenarios);

            m_ScenariosField.SetValue(TestScenarioSystem.instance, m_Scenarios);
        }

        private static IEnumerable<Type> GetTests() {
            return from t in Assembly.GetExecutingAssembly().GetTypes()
                where typeof(TestScenario).IsAssignableFrom(t)
                select t;
        }

        private void LoadNonEnglishLocalizations() {
            var thisAssembly  = Assembly.GetExecutingAssembly();
            var resourceNames = thisAssembly.GetManifestResourceNames();

            try {
                Log.Debug("Reading localizations");

                foreach (var localeID in GameManager.instance.localizationManager.GetSupportedLocales()) {
                    var resourceName = $"{thisAssembly.GetName().Name}.L10N.lang.{localeID}.json";
                    if (resourceNames.Contains(resourceName)) {
                        Log.Debug($"Found localization file {resourceName}");
                        try {
                            Log.Debug($"Reading embedded translation file {resourceName}");

                            // Read embedded file.
                            using StreamReader reader = new(thisAssembly.GetManifestResourceStream(resourceName));
                            {
                                var entireFile   = reader.ReadToEnd();
                                var varient      = JSON.Load(entireFile);
                                var translations = varient.Make<Dictionary<string, string>>();
                                GameManager.instance.localizationManager.AddSource(localeID, new MemorySource(translations));
                            }
                        } catch (Exception e) {
                            // Don't let a single failure stop us.
                            Log.Error(e, $"Exception reading localization from embedded file {resourceName}");
                        }
                    } else {
                        Log.Debug($"Did not find localization file {resourceName}");
                    }
                }
            } catch (Exception e) {
                Log.Error(e, "Exception reading embedded settings localization files");
            }
        }
    }
}
