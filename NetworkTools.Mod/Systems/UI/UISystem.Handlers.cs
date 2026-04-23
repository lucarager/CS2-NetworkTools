namespace NetworkTools.Systems.UI {
    using Game.Prefabs;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Connect;
    using NetworkTools.Systems.Tools.Generate;
    using NetworkTools.Systems.Tools.Parallel;
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

        private void HandleUpdateShapeConfig(ShapeTransformConfig configData) {
            m_Log.Debug($"HandleUpdateShapeConfig(template: {configData.Template})");

            var currentConfig = m_NtRoadShapeToolSystem.ShapeTransformConfig;
            if (currentConfig.Template == configData.Template) {
                m_ShapeConfigBinding.Value = configData;
                m_NtRoadShapeToolSystem.UpdateTransformationConfig(configData);
            } else {
                ShapeTransformConfig newConfig;

                // Create new config with default values
                switch (configData.Template) {
                    case ShapeTransformTemplate.Preserve:
                    default:
                        newConfig = ShapeTransformConfig.Preserve();
                        break;
                    case ShapeTransformTemplate.SlopeLinear:
                        newConfig = ShapeTransformConfig.SlopeLinear();
                        break;
                    case ShapeTransformTemplate.SlopeEaseInOut:
                        newConfig = ShapeTransformConfig.SlopeEaseInOut();
                        break;
                    case ShapeTransformTemplate.SlopeArch:
                        newConfig = ShapeTransformConfig.SlopeArch();
                        break;
                    case ShapeTransformTemplate.CurveStraighten:
                        newConfig = ShapeTransformConfig.CurveStraighten();
                        break;
                    case ShapeTransformTemplate.CurveSmooth:
                        newConfig = ShapeTransformConfig.CurveSmooth();
                        break;
                }

                m_NtRoadShapeToolSystem.SetTransformationConfig(newConfig);
                m_ShapeConfigBinding.Value = newConfig;
            }
        }

        private void HandleSelectTool(string id) {
            m_Log.Debug($"HandleSelectTool(id: {id})");

            if (m_PrefabSystem.TryGetPrefab(new PrefabID("NT_ToolPrefab",
                                                         id),
                                            out var prefab)) {
                m_ToolSystem.ActivatePrefabTool(prefab);

                // Sync shape config binding when the road shape tool resets its config
                if (m_ToolSystem.activeTool == m_NtRoadShapeToolSystem) {
                    m_ShapeConfigBinding.Value = m_NtRoadShapeToolSystem.ShapeTransformConfig;
                }

                // Sync grid config binding when the grid tool is activated
                if (m_ToolSystem.activeTool == m_NtGenerateToolSystem) {
                    m_GenerateConfigBinding.Value = m_NtGenerateToolSystem.CurrentConfig;
                }

                // Sync parallel config binding when the parallel tool is activated
                if (m_ToolSystem.activeTool == m_NtParallelToolSystem) {
                    m_ParallelConfigBinding.Value = m_NtParallelToolSystem.CurrentConfig;
                }
            }
        }

        private void HandleUpdateConnectMode(int mode) {
            m_Log.Debug($"HandleUpdateConnectMode(mode: {mode})");
            var connectMode = (ConnectMode)mode;
            m_NtConnectToolSystem.SetMode(connectMode);
            m_ConnectModeBinding.Value = mode;
        }

        private void HandleUpdateGenerateMode(int mode) {
            m_Log.Debug($"HandleUpdateGenerateMode(mode: {mode})");
            var generateMode = (GenerateMode)mode;
            m_NtGenerateToolSystem.SetMode(generateMode);
            m_GenerateModeBinding.Value = mode;
        }

        private void HandleUpdateGenerateConfig(GenerateConfig configData) {
            m_Log.Debug("HandleUpdateGenerateConfig");
            m_GenerateConfigBinding.Value = configData;
            m_NtGenerateToolSystem.UpdateConfig(configData);
        }

        private void HandleUpdateParallelConfig(ParallelConfig configData) {
            m_Log.Debug("HandleUpdateParallelConfig");
            m_ParallelConfigBinding.Value = configData;
            m_NtParallelToolSystem.UpdateConfig(configData);
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