// <copyright file="NetworkToolsMod.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using NetworkTools.Settings;

namespace NetworkTools {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Colossal;
    using Colossal.IO.AssetLookupbase;
    using Colossal.Localization;
    using Colossal.Logging;
    using Colossal.Reflection;
    using Colossal.TestFramework;
    using Colossal.UI;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;
    using NetworkTools.Systems;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// Mod entry point.
    /// </summary>
    public class NetworkToolsMod : IMod {
        /// <summary>
        /// The mod's default actionName.
        /// </summary>
        public const string ModName = "NetworkTools";

        /// <summary>
        /// An id used for bindings between UI and C#.
        /// </summary>
        public static readonly string Id = "NetworkTools";

        /// <summary>
        /// Gets the instance reference.
        /// </summary>
        public static NetworkToolsMod Instance {
            get; private set;
        }

        /// <summary>
        /// Gets the mod's settings configuration.
        /// </summary>
        internal NetworkToolsModSettings Settings {
            get; private set;
        }

        /// <summary>
        /// Gets the mod's logger.
        /// </summary>
        internal ILog Log {
            get; private set;
        }

        /// <summary>
        /// Sets mod to test mode
        /// </summary>
        internal bool IsTestMode {
            get;
            set;
        } = false;

        /// <summary>
        /// Gets the mod's version
        /// </summary>
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString(4);

        /// <summary>
        /// Gets the mod's informational version
        /// </summary>
        public static string InformationalVersion => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        /// <inheritdoc/>
        public void OnLoad(UpdateSystem updateSystem) {
            // Set instance reference.
            Instance = this;

            // Initialize logger.
            Log = LogManager.GetLogger(ModName);
#if IS_DEBUG
            Log.Info("[NetworkTools] Setting logging level to Debug");
            Log.effectivenessLevel = Level.Debug;
#endif
            Log.Info($"[NetworkTools] Loading {ModName} version {Assembly.GetExecutingAssembly().GetName().Version}");

            // Initialize Settings
            Settings = new NetworkToolsModSettings(this);

            // Load i18n
            GameManager.instance.localizationManager.AddSource("en-US", new EnUsConfig(Settings));
            Log.Info($"[NetworkTools] Loaded en-US.");
            LoadNonEnglishLocalizations();
            Log.Info($"[NetworkTools] Loaded localization files.");

            // Generate i18n files
#if IS_DEBUG && EXPORT_EN_US
            GenerateLanguageFile();
#endif

            // Register mod settings to game options UI.
            Settings.RegisterInOptionsUI();

            // Load saved settings.
            AssetLookupbase.global.LoadSettings("NetworkTools", Settings, new NetworkToolsModSettings(this));

            // Apply input bindings.
            Settings.RegisterKeyBindings();

            // Activate Systems
            updateSystem.UpdateAt<NT_PrefabsCreateSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<NT_AddNodeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<NT_UISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<NT_RenderSystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<NT_TooltipSystem>(SystemUpdatePhase.UITooltip);

            // Add tests
            AddTests();

            // Add mod UI resource directory to UI resource handler.
            if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out var modAsset)) {
                Log.Error($"Failed to get executable asset path. Exiting.");
                return;
            }

            var assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
            UIManager.defaultUISystem.AddHostLocation("nt", assemblyPath + "/Assets/");

            Log.Info($"Installed and enabled. RenderedFrame: {Time.renderedFrameCount}");
        }

        private void GenerateLanguageFile() {
            Log.Info($"[NetworkTools] Exporting localization");
            var localeDict = new EnUsConfig(Settings).ReadEntries(new List<IDictionaryEntryError>(), new Dictionary<string, int>()).ToDictionary(pair => pair.Key, pair => pair.Value);
            var str = JsonConvert.SerializeObject(localeDict, Newtonsoft.Json.Formatting.Indented);
            try {
                var path = "C:\\Users\\lucar\\source\\repos\\NetworkTools\\lang\\en-US.json";
                Log.Info($"[NetworkTools] Exporting to {path}");
                File.WriteAllText(path, str);
                path = "C:\\Users\\lucar\\source\\repos\\NetworkTools\\UI\\src\\lang\\en-US.json";
                Log.Info($"[NetworkTools] Exporting to {path}");
                File.WriteAllText(path, str);
            } catch (Exception ex) {
                Log.Error(ex.ToString());
            }
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

        private static void AddTests() {
            var log = LogManager.GetLogger(ModName);
            log.Debug($"AddTests()");

            var m_ScenariosField = typeof(TestScenarioSystem).GetField("m_Scenarios", BindingFlags.Instance | BindingFlags.NonPublic);
            if (m_ScenariosField == null) {
                log.Error("AddTests() -- Could not find m_Scenarios");
                return;
            }

            var m_Scenarios = (Dictionary<string, TestScenarioSystem.Scenario>)m_ScenariosField.GetValue(TestScenarioSystem.instance);

            foreach (var type in GetTests()) {
                if (!type.IsClass || type.IsAbstract || type.IsInterface || !type.TryGetAttribute(
                        out TestDescriptorAttribute testDescriptorAttribute, false)) {
                    continue;
                }

                log.Debug($"AddTests() -- {testDescriptorAttribute.description}");

                m_Scenarios.Add(testDescriptorAttribute.description, new TestScenarioSystem.Scenario {
                    category = testDescriptorAttribute.category,
                    testPhase = testDescriptorAttribute.testPhase,
                    test = type,
                    disabled = testDescriptorAttribute.disabled,
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
            var thisAssembly = Assembly.GetExecutingAssembly();
            var resourceNames = thisAssembly.GetManifestResourceNames();

            try {
                Log.Debug($"Reading localizations");

                foreach (var localeID in GameManager.instance.localizationManager.GetSupportedLocales()) {
                    var resourceName = $"{thisAssembly.GetName().Name}.lang.{localeID}.json";
                    if (resourceNames.Contains(resourceName)) {
                        Log.Debug($"Found localization file {resourceName}");
                        try {
                            Log.Debug($"Reading embedded translation file {resourceName}");

                            // Read embedded file.
                            using System.IO.StreamReader reader = new(thisAssembly.GetManifestResourceStream(resourceName));
                            {
                                var entireFile = reader.ReadToEnd();
                                var varient = Colossal.Json.JSON.Load(entireFile);
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
