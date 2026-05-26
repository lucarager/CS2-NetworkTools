namespace NetworkTools.Systems.UI {
    using Game.Prefabs;
    using NetworkTools.Systems.Tools;
    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_UISystem {
        /// <inheritdoc />
        protected override void OnUpdate() {
            // Update tool UI data when the prefab count changes
            var entityCount = m_ToolPrefabQuery.CalculateEntityCount();
            if (entityCount != m_LastToolPrefabCount) {
                m_LastToolPrefabCount = entityCount;
                var entities        = m_ToolPrefabQuery.ToEntityArray(Allocator.Temp);
                var toolPrefabArray = new NT_ToolPrefab[entities.Length];
                for (var i = 0; i < entities.Length; i++) {
                    toolPrefabArray[i] = m_PrefabSystem.GetPrefab<NT_ToolPrefab>(entities[i]);
                }

                m_ToolUIDataBinding.Value = toolPrefabArray;
            }

            // Update selected prefab binding when it changes
            var currentPrefab = m_ToolSystem.activePrefab != null
                                    ? m_ToolSystem.activePrefab.GetPrefabID().GetName()
                                    : "";
            if (currentPrefab != m_LastSelectedPrefab) {
                m_LastSelectedPrefab          = currentPrefab;
                m_SelectedPrefabBinding.Value = currentPrefab;
            }

            // Update selected entities binding when selection changes
            var selectedNodes = m_ToolSystem.activeTool is INodeSelectionProvider selectionProvider
                                    ? selectionProvider.GetSelectedNodes()
                                    : System.Array.Empty<Entity>();
            var currentNodesHash = ComputeSelectionHash(selectedNodes);
            if (currentNodesHash != m_LastSelectedNodesHash) {
                m_LastSelectedNodesHash = currentNodesHash;
                var selectedEntitiesData = new ToolSelectionData[selectedNodes.Length];

                for (var i = 0; i < selectedNodes.Length; i++) {
                    var entity     = selectedNodes[i];
                    var entityType = DetermineEntityType(entity);
                    var entityName = entityType == SelectedEntityType.Node
                                         ? GetComputedNodeName(entity, i)
                                         : m_NameSystem.GetRenderedLabelName(entity);
                    selectedEntitiesData[i] = new ToolSelectionData(entity, entityType, entityName);
                }

                m_SelectedEntitiesBinding.Value = selectedEntitiesData;
            }

            // Update distance unit binding when the setting changes
            var currentDistanceUnit = NetworkToolsMod.Instance.Settings.DistanceUnit;
            if (currentDistanceUnit != m_LastDistanceUnit) {
                m_LastDistanceUnit          = currentDistanceUnit;
                m_DistanceUnitBinding.Value = currentDistanceUnit;
            }

            // Update snap/target bindings from the active tool
            var activeTool = m_ToolSystem.activeTool as NT_BaseToolSystem;

            var currentAvailableSnaps = activeTool != null ? (int)activeTool.AvailableSnaps : (int)SnapOption.None;
            if (currentAvailableSnaps != m_LastAvailableSnaps) {
                m_LastAvailableSnaps          = currentAvailableSnaps;
                m_AvailableSnapsBinding.Value = currentAvailableSnaps;
            }

            var currentSelectedSnaps = activeTool != null ? (int)activeTool.SelectedSnaps : (int)SnapOption.None;
            if (currentSelectedSnaps != m_LastSelectedSnaps) {
                m_LastSelectedSnaps          = currentSelectedSnaps;
                m_SelectedSnapsBinding.Value = currentSelectedSnaps;
            }

            var currentAvailableTargets = activeTool != null ? (int)activeTool.AvailableTargets : (int)TargetOption.All;
            if (currentAvailableTargets != m_LastAvailableTargets) {
                m_LastAvailableTargets          = currentAvailableTargets;
                m_AvailableTargetsBinding.Value = currentAvailableTargets;
            }

            var currentSelectedTargets = activeTool != null ? (int)activeTool.SelectedTargets : (int)TargetOption.All;
            if (currentSelectedTargets != m_LastSelectedTargets) {
                m_LastSelectedTargets          = currentSelectedTargets;
                m_SelectedTargetsBinding.Value = currentSelectedTargets;
            }

            var currentAvailableViews = activeTool != null ? (int)activeTool.AvailableViews : (int)ViewOption.All;
            if (currentAvailableViews != m_LastAvailableViews) {
                m_LastAvailableViews          = currentAvailableViews;
                m_AvailableViewsBinding.Value = currentAvailableViews;
            }

            var currentSelectedViews = activeTool != null ? (int)activeTool.SelectedViews : (int)ViewOption.None;
            if (currentSelectedViews != m_LastSelectedViews) {
                m_LastSelectedViews          = currentSelectedViews;
                m_SelectedViewsBinding.Value = currentSelectedViews;
            }

            if (m_ToggleToolPanelAction.WasPerformedThisFrame()) {
                m_PanelOpenBinding.Value = true;
            }

            // Tool shortcuts are only active when the panel is open
            if (m_PanelOpenBinding.Value) {
                if (m_OpenTool1Action.WasPerformedThisFrame()) {
                    HandleSelectTool("AddNode");
                }

                if (m_OpenTool2Action.WasPerformedThisFrame()) {
                    HandleSelectTool("RemoveNode");
                }

                if (m_OpenTool3Action.WasPerformedThisFrame()) {
                    HandleSelectTool("SlideNode");
                }

                if (m_OpenTool4Action.WasPerformedThisFrame()) {
                    HandleSelectTool("SuperNode");
                }

                if (m_OpenTool5Action.WasPerformedThisFrame()) {
                    HandleSelectTool("ShapeSlope");
                }

                if (m_OpenTool6Action.WasPerformedThisFrame()) {
                    HandleSelectTool("ShapeCurve");
                }

                if (m_OpenTool7Action.WasPerformedThisFrame()) {
                    HandleSelectTool("Connect");
                }

                if (m_OpenTool8Action.WasPerformedThisFrame()) {
                    HandleSelectTool("Parallel");
                }

                if (m_OpenTool9Action.WasPerformedThisFrame()) {
                    HandleSelectTool("Generate");
                }

                if (m_ApplyTransformationAction.WasPerformedThisFrame()) {
                    HandleRequestApply();
                }
            }

            base.OnUpdate();
        }
    }
}