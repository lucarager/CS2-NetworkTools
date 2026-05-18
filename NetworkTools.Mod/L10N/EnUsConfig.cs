// <copyright file="EnUsConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.L10N {
    using System.Collections.Generic;

    using Colossal;

    using NetworkTools;
    using NetworkTools.Settings;

    /// <summary>
    /// Configures the English (US) localization for NetworkTools Mod.
    /// </summary>
    public class EnUsConfig : IDictionarySource {
        private readonly Dictionary<string, string> m_Localization;
        private readonly NT_Settings                m_Setting;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnUsConfig"/> class.
        /// </summary>
        /// <param name="setting">NetworkToolsModSettings.</param>
        public EnUsConfig(NT_Settings setting) {
            m_Setting = setting;

            m_Localization = new Dictionary<string, string> {
                { m_Setting.GetSettingsLocaleID(), NetworkToolsMod.Id },

                // Actions
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.ToggleToolPanelStr), "Toggle Network Tools Panel" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.ToggleToolPanelStr), "Opens the NT panel" },
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.OpenTool1Str), "Open Add Node" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.OpenTool1Str), "Shortcut to open a specific Network Tools tool, use it after opening the NT panel" },
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.OpenTool2Str), "Open Remove Node" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.OpenTool2Str), "Shortcut to open a specific Network Tools tool, use it after opening the NT panel" },
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.OpenTool3Str), "Open Slide Node" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.OpenTool3Str), "Shortcut to open a specific Network Tools tool, use it after opening the NT panel" },
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.OpenTool4Str), "Open Super Node" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.OpenTool4Str), "Shortcut to open a specific Network Tools tool, use it after opening the NT panel" },
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.OpenTool5Str), "Open Slope Tools" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.OpenTool5Str), "Shortcut to open a specific Network Tools tool, use it after opening the NT panel" },
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.OpenTool6Str), "Open Curve Tools" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.OpenTool6Str), "Shortcut to open a specific Network Tools tool, use it after opening the NT panel" },
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.OpenTool7Str), "Open Connect Tools" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.OpenTool7Str), "Shortcut to open a specific Network Tools tool, use it after opening the NT panel" },
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.OpenTool8Str), "Open Parallel Tool" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.OpenTool8Str), "Shortcut to open a specific Network Tools tool, use it after opening the NT panel" },
                { m_Setting.GetOptionLabelLocaleID(NT_Settings.OpenTool9Str), "Open Generate Tool" },
                { m_Setting.GetOptionDescLocaleID(NT_Settings.OpenTool9Str), "Shortcut to open a specific Network Tools tool, use it after opening the NT panel" },

                // Sections

                // Groups
                { m_Setting.GetOptionGroupLocaleID(NT_Settings.KeybindingsGroupStr), "Key Bindings" },
                { m_Setting.GetOptionGroupLocaleID(NT_Settings.AboutGroupStr), "About" },

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

                // Hint Tooltips - Common
                { "Common.ACTION[NetworkTools.HintTooltip.Common.Exit]", "Exit Tool" },

                // # Tool Strings
                // ## AddNode
                // ### AddNode - Metadata
                { "NetworkTools.Tools.AddNode.Name", "Add Node" },
                { "NetworkTools.Tools.AddNode.Description", "Allows adding a node to a segment, dividing the segment into two." },
                // ### AddNode - Hint Tooltips
                { "Common.ACTION[NetworkTools.HintTooltip.AddNode.Hover]", "Hover over a network segment" },
                { "Common.ACTION[NetworkTools.HintTooltip.AddNode.Apply]", "Add node" },
                { "Common.ACTION[NetworkTools.HintTooltip.AddNode.Cancel]", "Cancel" },

                // ## RemoveNode
                // ### RemoveNode - Metadata
                { "NetworkTools.Tools.RemoveNode.Name", "Remove Node" },
                { "NetworkTools.Tools.RemoveNode.Description", "Allows removing a node from a network." },
                // ### RemoveNode - Hint Tooltips
                { "Common.ACTION[NetworkTools.HintTooltip.RemoveNode.Select]", "Select a node to remove" },
                { "Common.ACTION[NetworkTools.HintTooltip.RemoveNode.Apply]", "Remove node" },
                { "Common.ACTION[NetworkTools.HintTooltip.RemoveNode.Cancel]", "Cancel" },

                // ## ShapeSlope
                // ### ShapeSlope - Metadata
                { "NetworkTools.Tools.ShapeSlope.Name", "Slope Tools" },
                { "NetworkTools.Tools.ShapeSlope.Description", "Allows editing the slope of a contiguous path." },
                // ### ShapeSlope - Hint Tooltips
                { "Common.ACTION[NetworkTools.HintTooltip.ShapeSlope.SelectStart]", "Select a starting node" },
                { "Common.ACTION[NetworkTools.HintTooltip.ShapeSlope.SelectSecond]", "Select a second node" },
                { "Common.ACTION[NetworkTools.HintTooltip.ShapeSlope.RemoveLast]", "Remove last node" },
                { "Common.ACTION[NetworkTools.HintTooltip.ShapeSlope.ExtendPath]", "Select a new end node to extend the path" },

                // ## ShapeCurve
                // ### ShapeCurve - Metadata
                { "NetworkTools.Tools.ShapeCurve.Name", "Curve Tools" },
                { "NetworkTools.Tools.ShapeCurve.Description", "Allows editing the curve of a contiguous path." },
                // ### ShapeCurve - Hint Tooltips
                { "Common.ACTION[NetworkTools.HintTooltip.ShapeCurve.SelectStart]", "Select a starting node" },
                { "Common.ACTION[NetworkTools.HintTooltip.ShapeCurve.SelectSecond]", "Select a second node" },
                { "Common.ACTION[NetworkTools.HintTooltip.ShapeCurve.RemoveLast]", "Remove last node" },
                { "Common.ACTION[NetworkTools.HintTooltip.ShapeCurve.ExtendPath]", "Select a new end node to extend the path" },
                
                // ## SlideNode
                // ### SlideNode - Metadata
                { "NetworkTools.Tools.SlideNode.Name", "Slide Node" },
                { "NetworkTools.Tools.SlideNode.Description", "Allows sliding nodes along existing edges." },

                // ## SuperNode
                // ### SuperNode - Metadata
                { "NetworkTools.Tools.SuperNode.Name", "Super Node" },
                { "NetworkTools.Tools.SuperNode.Description", "Allows combining multiple nodes into one large intersection." },
                // ### SuperNode - Hint Tooltips
                { "Common.ACTION[NetworkTools.HintTooltip.SuperNode.SelectStart]", "Select a node" },
                { "Common.ACTION[NetworkTools.HintTooltip.SuperNode.SelectSecond]", "Add another node" },
                { "Common.ACTION[NetworkTools.HintTooltip.SuperNode.RemoveLast]", "Remove last node" },

                // ## Connect
                // ### Connect - Metadata
                { "NetworkTools.Tools.Connect.Name", "Connect Tools" },
                { "NetworkTools.Tools.Connect.Description", "Allows creating a new connection between two nodes in a number of ways." },

                // ## Parallel
                // ### Parallel - Metadata
                { "NetworkTools.Tools.Parallel.Name", "Parallel Tool" },
                { "NetworkTools.Tools.Parallel.Description", "Allows creating perfect parallel networks from a source network." },
                // ### Parallel - Hint Tooltips
                { "Common.ACTION[NetworkTools.HintTooltip.Parallel.SelectStart]", "Select a starting node" },
                { "Common.ACTION[NetworkTools.HintTooltip.Parallel.SelectSecond]", "Select a second node" },
                { "Common.ACTION[NetworkTools.HintTooltip.Parallel.RemoveLast]", "Remove last node" },
                { "Common.ACTION[NetworkTools.HintTooltip.Parallel.ExtendPath]", "Select a new end node to extend the path" },

                // ## Generate
                // ### Generate - Metadata
                { "NetworkTools.Tools.Generate.Name", "Generate Tool" },
                { "NetworkTools.Tools.Generate.Description", "Allows generating a variety of networks such as perfect road grids and circles." },
                // ### Generate - Hint Tooltips
                { "Common.ACTION[NetworkTools.HintTooltip.Generate.Place]", "Place network origin" },
                { "Common.ACTION[NetworkTools.HintTooltip.Generate.Rotate]", "Rotate" },
                { "Common.ACTION[NetworkTools.HintTooltip.Generate.RemovePlacement]", "Remove placement" },

                // # UI Strings
                // ## Common
                { "NetworkTools.UI.Common.NetworkTools", "Network Tools" },
                { "NetworkTools.UI.Common.Mode", "Mode" },
                { "NetworkTools.UI.Common.ToggleAll", "Toggle All" },
                { "NetworkTools.UI.Common.SelectAtLeastTwoNodes", "Select at least two nodes." },
                { "NetworkTools.UI.Common.HowToUse", "How to use" },
                { "NetworkTools.UI.Common.Tutorial", "Select the tool to configure it. Adjust snapping, target selection, and view mode using the options in the panel. Each tool provides its own specific parameters below." },
                { "NetworkTools.UI.Common.ComingSoon", "Coming Soon!" },

                // ## Prefab Search
                { "NetworkTools.UI.PrefabSearch.Title", "Select Asset" },
                { "NetworkTools.UI.PrefabSearch.Placeholder", "Search assets..." },
                { "NetworkTools.UI.PrefabSearch.Empty", "No assets found." },
                { "NetworkTools.UI.PrefabSearch.NetworkPrefab", "Asset" },

                // ## Prefab Tabs
                { "NetworkTools.UI.PrefabTab.Road", "Road" },
                { "NetworkTools.UI.PrefabTab.Path", "Path" },
                { "NetworkTools.UI.PrefabTab.Rail", "Rail" },
                { "NetworkTools.UI.PrefabTab.Waterway", "Waterway" },
                { "NetworkTools.UI.PrefabTab.NetLane", "NetLane" },

                // ## View Options
                { "NetworkTools.UI.View.Label", "View" },
                { "NetworkTools.UI.View.Underground", "Underground" },
                { "NetworkTools.UI.View.ZoneGrid", "Zone Grid" },
                { "NetworkTools.UI.View.InvisibleNetworks", "Invisible Networks" },

                // ## Target Options
                { "NetworkTools.UI.Target.Label", "Targets" },
                { "NetworkTools.UI.Target.Road", "Road" },
                { "NetworkTools.UI.Target.Path", "Path" },
                { "NetworkTools.UI.Target.Rail", "Rail" },
                { "NetworkTools.UI.Target.Waterway", "Waterway" },
                { "NetworkTools.UI.Target.InvisiblePath", "InvisiblePath" },

                // ## Snap Options
                { "NetworkTools.UI.Snap.Label", "Snapping" },
                { "NetworkTools.UI.Snap.ZoneGrid", "Zone Grid" },
                { "NetworkTools.UI.Snap.MidPoint", "Mid Point" },

                // ## Slope Tool
                { "NetworkTools.UI.Slope.Preserve", "Preserve" },
                { "NetworkTools.UI.Slope.ConstantSlope", "Constant Slope" },
                { "NetworkTools.UI.Slope.EaseInOutSlope", "EaseInOut Slope" },
                { "NetworkTools.UI.Slope.StartingFlatness", "Starting Flatness" },
                { "NetworkTools.UI.Slope.EndingFlatness", "Ending Flatness" },
                { "NetworkTools.UI.Slope.ArchHeight", "Arch Height" },
                { "NetworkTools.UI.Slope.ArchPosition", "Arch Position" },
                { "NetworkTools.UI.Slope.ApplySlope", "Apply Slope" },

                // ## Curve Tool
                { "NetworkTools.UI.Curve.Preserve", "Preserve" },
                { "NetworkTools.UI.Curve.StraightenCurve", "Straighten Curve" },
                { "NetworkTools.UI.Curve.SmoothCurve", "Smooth Curve" },
                { "NetworkTools.UI.Curve.SmoothingFactor", "Smoothing Factor" },
                { "NetworkTools.UI.Curve.ApplyCurve", "Apply Transformation" },

                // ## Connect Tool
                { "NetworkTools.UI.Connect.None", "None" },
                { "NetworkTools.UI.Connect.SimpleCurve", "Simple Curve" },
                { "NetworkTools.UI.Connect.ComplexCurve", "Complex Curve" },
                { "NetworkTools.UI.Connect.Loop", "Loop" },
                { "NetworkTools.UI.Connect.LoopRadius", "Loop Radius" },
                { "NetworkTools.UI.Connect.ApplyCurve", "Apply Curve" },

                // ## SuperNode Tool
                { "NetworkTools.UI.SuperNode.CreateSupernode", "Create Supernode" },

                // ## Parallel Tool
                { "NetworkTools.UI.Parallel.HorizontalOffset", "Horizontal Offset" },
                { "NetworkTools.UI.Parallel.VerticalOffset", "Vertical Offset" },
                { "NetworkTools.UI.Parallel.Direction", "Direction" },
                { "NetworkTools.UI.Parallel.Same", "Same" },
                { "NetworkTools.UI.Parallel.Reverse", "Reverse" },
                { "NetworkTools.UI.Parallel.Origin", "Origin" },
                { "NetworkTools.UI.Parallel.LeftEdge", "Left Edge" },
                { "NetworkTools.UI.Parallel.Center", "Center" },
                { "NetworkTools.UI.Parallel.RightEdge", "Right Edge" },
                { "NetworkTools.UI.Parallel.CreateParallel", "Create parallel network" },

                // ## Generate Tool
                { "NetworkTools.UI.Generate.Grid", "Grid" },
                { "NetworkTools.UI.Generate.Circle", "Circle" },
                { "NetworkTools.UI.Generate.XSpacing", "X Spacing" },
                { "NetworkTools.UI.Generate.ZSpacing", "Z Spacing" },
                { "NetworkTools.UI.Generate.XCount", "X Count" },
                { "NetworkTools.UI.Generate.ZCount", "Z Count" },
                { "NetworkTools.UI.Generate.Radius", "Radius" },
                { "NetworkTools.UI.Generate.Elevation", "Elevation" },
                //{ "NetworkTools.UI.Generate.AltPrefabX", "Alternate Prefab X" },
                //{ "NetworkTools.UI.Generate.AltPrefabZ", "Alternate Prefab Z" },
                { "NetworkTools.UI.Generate.Apply", "Generate" },
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
