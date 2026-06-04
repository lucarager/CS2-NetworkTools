namespace NetworkTools.Systems.UI {
    using Game.Prefabs;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Generate;
    using NetworkTools.Systems.Tools.RoadShape;

    public partial class NT_UISystem {
        private void HandlePanelOpen(bool value) {
            m_Log.Debug($"HandlePanelOpen(value: {value})");
            m_PanelOpenBinding.Value = value;

            // Close active tool when panel is closed
            if (!value && m_ToolSystem.activeTool is NT_BaseToolSystem activeTool) {
                activeTool.RequestDisable();
            }
        }

        private void HandleSelectTool(string id) {
            m_Log.Debug($"HandleSelectTool(id: {id})");

            if (m_PrefabSystem.TryGetPrefab(new PrefabID("NT_ToolPrefab",
                                                         id),
                                            out var prefab)) {
                m_ToolSystem.ActivatePrefabTool(prefab);

                // Re-sync per-parameter bindings when a parameterized tool is activated
                if (m_ToolSystem.activeTool == m_NtRoadShapeToolSystem) {
                    foreach (var p in m_NtRoadShapeToolSystem.Parameters)
                        p.ForceNotify();
                }

                if (m_ToolSystem.activeTool == m_NtGenerateToolSystem) {
                    foreach (var p in m_NtGenerateToolSystem.Parameters)
                        p.ForceNotify();
                }

                if (m_ToolSystem.activeTool == m_NtConnectToolSystem) {
                    foreach (var p in m_NtConnectToolSystem.Parameters)
                        p.ForceNotify();
                }

                if (m_ToolSystem.activeTool == m_NtParallelToolSystem) {
                    foreach (var p in m_NtParallelToolSystem.Parameters)
                        p.ForceNotify();
                }
            }
        }

        private void HandleRequestApply() {
            m_Log.Debug($"HandleRequestApply() -- validRequest={m_ToolSystem.activeTool is IManualApplyProvider}");

            if (m_ToolSystem.activeTool is IManualApplyProvider activeTool) {
                activeTool.RequestApply();
            }
        }

        private void HandleUpdateSelectedSnaps(int value) {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem activeTool) {
                return;
            }

            activeTool.SelectedSnaps = (SnapOption)value;

            var settings = NetworkToolsMod.Instance?.Settings;
            if (settings != null) {
                settings.SavedSelectedSnaps = value;
                settings.ApplyAndSave();
            }
        }

        private void HandleUpdateSelectedTargets(int value) {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem activeTool) {
                return;
            }

            activeTool.SelectedTargets = (TargetOption)value;
            activeTool.RefreshEligibility();

            var settings = NetworkToolsMod.Instance?.Settings;
            if (settings != null) {
                settings.SavedSelectedTargets = value;
                settings.ApplyAndSave();
            }
        }

        private void HandleUpdateAnarchyEnabled(bool value) {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem activeTool || !activeTool.SupportsAnarchy) {
                return;
            }

            activeTool.AnarchyEnabled = value;
            activeTool.RefreshAnarchy();

            var settings = NetworkToolsMod.Instance?.Settings;
            if (settings != null) {
                settings.SavedAnarchyEnabled = value;
                settings.ApplyAndSave();
            }
        }

        private void HandleUpdateSelectedViews(int value) {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem activeTool) {
                return;
            }

            activeTool.SelectedViews = (ViewOption)value;
            activeTool.RefreshViews();

            var settings = NetworkToolsMod.Instance?.Settings;
            if (settings != null) {
                settings.SavedSelectedViews = value;
                settings.ApplyAndSave();
            }
        }
    }
}