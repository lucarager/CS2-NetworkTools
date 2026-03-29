namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Colossal.Entities;
    using Game.Common;
    using Game.Prefabs;
    using NetworkTools.Components;
    using NetworkTools.Components.Tools;
    using Unity.Collections;
    using Unity.Entities;

    #endregion

    public partial class NT_SuperNodeToolSystem {
        /// <inheritdoc />
        public bool HasToolComponent(PrefabBase prefab) {
            return m_PrefabSystem.HasComponent<NT_SuperNodeTool>(prefab);
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_SuperNodeToolSystem);

            // Configuration
            RenderEligibleNodes        = true;
            DisableVanillaValidation   = true;
            UseCustomEligibilityFilter = true;

            // Data structures
            m_SelectedNodes = new NativeList<Entity>(32, Allocator.Persistent);
        }

        protected override void OnDestroy() {
            if (m_SelectedNodes.IsCreated) {
                m_SelectedNodes.Dispose();
            }

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            // Ensure clean state
            ResetToIdle();

            MarkEligibleEntities();
        }

        /// <inheritdoc />
        protected override bool FilterEligibleEntity(Entity entity) {
            // Ensure that the node has the same owner as the others (if any are selected)
            if (m_SelectedNodes.Length == 0) {
                return true;
            }

            var aSelectedNode   = m_SelectedNodes[0];
            var currentHasOwner = EntityManager.TryGetComponent<Owner>(aSelectedNode, out var existingOwner);
            var newHasOwner     = EntityManager.TryGetComponent<Owner>(entity,        out var newOwner);

            // Exclude different owner results, or different owners
            return currentHasOwner == newHasOwner && existingOwner.m_Owner == newOwner.m_Owner;
        }

        protected override void OnStopRunning() {
            m_Log.Debug("OnStopRunning: Cleaning up state components");

            // Clear state
            ResetToIdle();

            base.OnStopRunning();
        }

        private void ResetToIdle() {
            // Clear state
            EntityManager.RemoveComponent<NT_Selected>(m_AllNtComponentsQuery);
            m_SelectedNodes.Clear();

            UpdatePhase();
        }
    }
}