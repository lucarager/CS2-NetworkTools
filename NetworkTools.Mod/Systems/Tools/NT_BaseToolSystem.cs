// <copyright file="NT_BaseToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Input;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    #endregion


    /// <summary>
    ///     Represents the phase of the current transformation operation.
    /// </summary>
    public enum OperationPhase {
        Idle = 0, // No operation configured
        Configuring = 1, // Operation configured but insufficient selection
        Ready = 2, // Operation configured with valid selection
        Applying = 3 // Operation is being applied 
    }

    /// <summary>
    ///     Determines how the transformation job outputs its results.
    /// </summary>
    public enum ToolOutputMode : byte {
        /// <summary>
        ///     Create CreationDefinition + NetCourse entities for preview.
        /// </summary>
        Preview,

        /// <summary>
        ///     Modify existing Curve components and handle intersection adjustments.
        /// </summary>
        Apply
    }

    /// <summary>
    ///     Base tool system
    /// </summary>
    public abstract partial class NT_BaseToolSystem : ToolBaseSystem {
        /// <summary>
        ///     Maximum distance to select a node when selecting near an edge
        /// </summary>
        protected const float MaxDistanceToSelect = 16f;

        /// <summary>
        ///     Tool requests disabling vanilla NodeReductionSystem during lifecycle
        /// </summary>
        public bool DisableVanillaNodeReduction = false;

        /// <summary>
        ///     Tool requests disabling vanilla validation during lifecycle
        /// </summary>
        public bool DisableVanillaValidation = false;

        /// <summary>
        ///     Apply action (usually left click)
        /// </summary>
        internal IProxyAction m_ApplyAction;

        /// <summary>
        ///     Common systems used by derived tools
        /// </summary>
        protected ToolOutputBarrier m_Barrier;

        /// <summary>
        ///     Common entity queries for node management
        /// </summary>
        protected EntityQuery m_DefinitionQuery;

        protected EntityQuery m_EdgesWithHighlightedQuery;
        protected EntityQuery m_EdgesWithSelectedQuery;
        protected EntityQuery m_EntitiesWithHighlightedQuery;

        /// <summary>
        ///     Native collections for tracking entities
        /// </summary>
        protected NativeReference<Entity> m_LastHoveredEntity;

        protected NativeReference<Entity> m_LastRaycastEntity;

        internal PrefixedLogger m_Log;
        protected EntityQuery m_NodesWithEligibleQuery;
        protected EntityQuery m_NodesWithHighlightedQuery;
        protected EntityQuery m_NodesWithoutEligibleQuery;
        protected OverlayRenderSystem m_OverlayRenderSystem;

        /// <summary>
        ///     Selected Prefab, set by derived tools
        /// </summary>
        protected PrefabBase m_Prefab;

        /// <summary>
        ///     Secondary apply action (usually right click)
        /// </summary>
        internal IProxyAction m_SecondaryApplyAction;

        protected TerrainSystem m_TerrainSystem;

        /// <summary>
        ///     Vanilla ValidationSystem
        /// </summary>
        protected ValidationSystem m_ValidationSystem;

        /// <summary>
        ///     Vanilla NodeReductionSystem
        /// </summary>
        protected NodeReductionSystem m_NodeReductionSystem;

        /// <summary>
        ///     Phase
        /// </summary>
        public OperationPhase Phase = OperationPhase.Idle;

        /// <summary>
        ///     Tool requests rendering edges
        /// </summary>
        public bool RenderEligibleEdges = false;

        /// <summary>
        ///     Tool requests rendering nodes
        /// </summary>
        public bool RenderEligibleNodes = false;

        /// <summary>
        ///     Tool requests rendering markers
        /// </summary>
        public bool RenderHandles = false;

        /// <summary>
        ///     Tool requests rendering tooltips of slopes for selected edges
        /// </summary>
        public bool RenderSlopeTooltips = false;

        /// <summary>
        ///     Tool requests rendering temp edges
        /// </summary>
        public bool RenderTempEdges = false;

        public override string toolID => "NT_BaseToolSystem";

        protected override void OnCreate() {
            base.OnCreate();

            // Start disabled - tools must be explicitly enabled
            Enabled = false;

            // Logging
            m_Log = new PrefixedLogger(nameof(NT_BaseToolSystem));
            m_Log.Debug("OnCreate()");

            // Systems
            m_ValidationSystem    = World.GetOrCreateSystemManaged<ValidationSystem>();
            m_NodeReductionSystem = World.GetOrCreateSystemManaged<NodeReductionSystem>();
            m_Barrier             = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem       = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();

            // Actions
            m_ApplyAction          = applyAction;
            m_SecondaryApplyAction = secondaryApplyAction;

            // Native Collections 
            m_LastHoveredEntity = new NativeReference<Entity>(Allocator.Persistent);
            m_LastRaycastEntity = new NativeReference<Entity>(Allocator.Persistent);

            // Initialize handle management
            InitializeHandles();

            // Queries
            m_DefinitionQuery = GetDefinitionQuery();
            m_NodesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                .WithAll<Node>()
                .WithNone<NT_Eligible>()
                .Build();
            m_NodesWithEligibleQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, NT_Eligible>()
                .Build();
            m_NodesWithHighlightedQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, NT_Highlighted>()
                .Build();
            m_EntitiesWithHighlightedQuery = SystemAPI.QueryBuilder()
                .WithAll<NT_Highlighted>()
                .Build();
        }

        protected override void OnDestroy() {
            // Dispose handle management
            DisposeHandles();

            // Dispose native collections
            if (m_LastHoveredEntity.IsCreated) {
                m_LastHoveredEntity.Dispose();
            }

            if (m_LastRaycastEntity.IsCreated) {
                m_LastRaycastEntity.Dispose();
            }

            base.OnDestroy();
        }

        public override PrefabBase GetPrefab() {
            return m_Prefab;
        }

        public void RequestEnable() {
            m_ToolSystem.activeTool = this;
        }

        public void RequestDisable() {
            m_ToolSystem.activeTool = m_DefaultToolSystem;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            return inputDeps;
        }

        protected override void OnStartRunning() {
            if (DisableVanillaValidation) {
                m_ValidationSystem.Enabled = false;
            }

            if (DisableVanillaNodeReduction) {
                m_NodeReductionSystem.Enabled = false;
            }

            // Reset tracking
            if (m_LastHoveredEntity.IsCreated) {
                m_LastHoveredEntity.Value = Entity.Null;
            }

            if (m_LastRaycastEntity.IsCreated) {
                m_LastRaycastEntity.Value = Entity.Null;
            }

            // Enable actions
            UpdateActions();
        }

        protected override void OnStopRunning() {
            // Disable actions
            m_ApplyAction.shouldBeEnabled          = false;
            m_SecondaryApplyAction.shouldBeEnabled = false;

            if (DisableVanillaValidation) {
                m_ValidationSystem.Enabled = true;
            }

            if (DisableVanillaNodeReduction) {
                m_NodeReductionSystem.Enabled = true;
            }

            // Cleanup handles
            CleanupHandles();

            // Cleanup highlights
            CleanupHighlights();
        }


        /// <summary>
        ///     Performs common cleanup operations when tool stops running.
        ///     Override to add tool-specific cleanup, but call base implementation.
        /// </summary>
        protected virtual void CleanupHighlights() {
            if (m_NodesWithEligibleQuery.IsEmptyIgnoreFilter) {
                return;
            }

            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
        }

        /// <summary>
        ///     Updates action enabled states. Override to customize action behavior.
        /// </summary>
        protected virtual void UpdateActions() {
            m_ApplyAction.shouldBeEnabled          = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
        }

        /// <summary>
        ///     Adds NT_Highlighted component to an entity.
        /// </summary>
        protected virtual void AddHighlight(Entity entity, NT_Highlighted highlightData) {
            if (entity == Entity.Null) {
                return;
            }

            EntityManager.AddComponentData(entity, highlightData);
        }

        /// <summary>
        ///     Removes NT_Highlighted component from an entity.
        /// </summary>
        protected virtual void RemoveHighlight(Entity entity) {
            if (entity == Entity.Null) {
                return;
            }

            EntityManager.RemoveComponent<NT_Highlighted>(entity);
        }

        /// <summary>
        ///     Swaps highlighting between two entities (removes from old, adds to new).
        /// </summary>
        protected virtual void
            SwapHighlightedEntities(Entity oldEntity, Entity newEntity, NT_Highlighted highlightData) {
            RemoveHighlight(oldEntity);
            AddHighlight(newEntity, highlightData);
        }

        /// <summary>
        ///     Clears all NT_Highlighted components from nodes (batch operation).
        /// </summary>
        protected virtual void ClearAllHighlights() {
            if (m_EntitiesWithHighlightedQuery.IsEmptyIgnoreFilter) {
                return;
            }

            EntityManager.RemoveComponent<NT_Highlighted>(m_EntitiesWithHighlightedQuery);
        }
    }
}