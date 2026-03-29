namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using System.Collections.Generic;
    using Game.Common;
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
        Idle        = 0, // No operation configured
        Configuring = 1, // Operation configured but insufficient selection
        Ready       = 2, // Operation configured with valid selection
        Applying    = 3 // Operation is being applied 
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

        protected ComponentTypeSet AllNtComponentsTypeSet = new(typeof(NT_Eligible),
                                                                typeof(NT_Highlighted),
                                                                typeof(NT_Selected),
                                                                typeof(NT_SelectedFirst),
                                                                typeof(NT_SelectedLast));

        /// <summary>
        ///     Tool requests disabling vanilla NodeReductionSystem during lifecycle
        /// </summary>
        public bool DisableVanillaNodeReduction = false;

        /// <summary>
        ///     Tool requests disabling vanilla validation during lifecycle
        /// </summary>
        public bool DisableVanillaValidation = false;

        /// <summary>
        ///     Which entity type this tool marks as eligible (Node or Edge).
        ///     Set to <see cref="EligibilityTarget.Edge" /> for tools that operate on edges.
        /// </summary>
        protected EligibilityTarget EligibilityTarget = EligibilityTarget.Node;

        protected ComponentTypeSet HighlightedComponentTypeSet = new(typeof(NT_Highlighted), typeof(Highlighted));
        protected EntityQuery      m_AllNtComponentsQuery;

        /// <summary>
        ///     Apply action (usually left click)
        /// </summary>
        internal IProxyAction m_ApplyAction;

        /// <summary>
        ///     Barrier
        /// </summary>
        protected ToolOutputBarrier m_Barrier;

        // Container query from vanilla
        protected EntityQuery m_ContainerQuery;

        /// <summary>
        ///     Common entity queries for node management
        /// </summary>
        protected EntityQuery m_DefinitionQuery;

        /// <summary>
        ///     Per-target-flag queries for adding NT_Eligible to matching edges.
        /// </summary>
        private EntityQuery m_EdgesWithEligibleQuery;

        protected EntityQuery m_EdgesWithHighlightedQuery;
        private   EntityQuery m_EdgesWithoutEligibleQuery;
        protected EntityQuery m_EdgesWithSelectedQuery;
        protected EntityQuery m_EntitiesWithHighlightedQuery;

        /// <summary>
        ///     Native collections for tracking entities
        /// </summary>
        protected NativeReference<Entity> m_LastHoveredEntity;

        protected NativeReference<Entity> m_LastRaycastEntity;

        internal PrefixedLogger m_Log;

        /// <summary>
        ///     Vanilla NodeReductionSystem
        /// </summary>
        protected NodeReductionSystem m_NodeReductionSystem;

        protected EntityQuery         m_NodesWithEligibleQuery;
        protected EntityQuery         m_NodesWithHighlightedQuery;
        protected EntityQuery         m_NodesWithoutEligibleQuery;
        protected EntityQuery         m_NodesWithSelectedFirstQuery;
        protected EntityQuery         m_NodesWithSelectedLastQuery;
        protected EntityQuery         m_NodesWithSelectedQuery;
        protected OverlayRenderSystem m_OverlayRenderSystem;

        /// <summary>
        ///     Selected Prefab, set by derived tools
        /// </summary>
        protected PrefabBase m_Prefab;

        /// <summary>
        ///     Tool System
        /// </summary>
        protected RenderingSystem m_RenderingSystem;

        /// <summary>
        ///     Secondary apply action (usually right click)
        /// </summary>
        internal IProxyAction m_SecondaryApplyAction;

        /// <summary>
        ///     Selected net lane prefab for parallel road segments.
        /// </summary>
        protected NetLanePrefab m_SelectedNetLanePrefab;

        /// <summary>
        ///     Selected net lane prefab entity.
        /// </summary>
        protected Entity m_SelectedNetLanePrefabEntity;

        /// <summary>
        ///     Selected net prefab for parallel road segments.
        /// </summary>
        protected NetPrefab m_SelectedNetPrefab;

        /// <summary>
        ///     Selected net prefab entity.
        /// </summary>
        protected Entity m_SelectedNetPrefabEntity;

        private EntityQuery m_TargetInvisiblePathEdgesQuery;
        private EntityQuery m_TargetInvisiblePathNodesQuery;
        private EntityQuery m_TargetPathEdgesQuery;
        private EntityQuery m_TargetPathNodesQuery;
        private EntityQuery m_TargetRailEdgesQuery;
        private EntityQuery m_TargetRailNodesQuery;
        private EntityQuery m_TargetRoadEdgesQuery;

        /// <summary>
        ///     Per-target-flag queries for adding NT_Eligible to matching nodes.
        /// </summary>
        private EntityQuery m_TargetRoadNodesQuery;

        private EntityQuery m_TargetWaterwayEdgesQuery;
        private EntityQuery m_TargetWaterwayNodesQuery;

        protected TerrainSystem m_TerrainSystem;

        /// <summary>
        ///     Tool System
        /// </summary>
        protected ToolSystem m_ToolSystem;

        protected EntityQuery m_UnselectedNodesWithoutEligibleQuery;

        /// <summary>
        ///     Vanilla ValidationSystem
        /// </summary>
        protected ValidationSystem m_ValidationSystem;

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

        /// <summary>
        ///     Tool requests rendering temp nodes
        /// </summary>
        public bool RenderTempNodes = false;

        /// <summary>
        ///     Whether this tool uses custom per-entity eligibility filtering.
        ///     When true, MarkEligibleNodes will call FilterEligibleEntity for each candidate.
        /// </summary>
        protected bool UseCustomEligibilityFilter = false;

        /// <summary>
        ///     Snap options this tool makes available to the player.
        ///     Override in derived tools to expose specific snap options.
        /// </summary>
        public virtual SnapOption AvailableSnaps => SnapOption.None;

        /// <summary>
        ///     Currently active snap options selected by the player.
        /// </summary>
        public SnapOption SelectedSnaps { get; set; } = SnapOption.None;

        /// <summary>
        ///     Target options this tool makes available to the player.
        ///     Override in derived tools to expose specific target options.
        /// </summary>
        public virtual TargetOption AvailableTargets => TargetOption.All;

        /// <summary>
        ///     Currently active target options selected by the player.
        /// </summary>
        public TargetOption SelectedTargets { get; set; } = TargetOption.Default;

        /// <summary>
        ///     View options this tool makes available to the player.
        ///     Override in derived tools to expose specific view options.
        /// </summary>
        public virtual ViewOption AvailableViews => ViewOption.All;

        /// <summary>
        ///     Currently active view options selected by the player.
        /// </summary>
        public ViewOption SelectedViews { get; set; } = ViewOption.None;

        /// <summary>
        ///     Gets the currently selected net prefab, or <c>null</c> if none is selected.
        /// </summary>
        public PrefabBase SelectedNetPrefab {
            get {
                if (m_SelectedNetLanePrefab != null) {
                    return m_SelectedNetLanePrefab;
                }

                return m_SelectedNetPrefab;
            }
        }

        /// <summary>
        ///     Gets the ECS entity of the selected net prefab, or <see cref="Entity.Null" /> if none.
        /// </summary>
        public Entity SelectedNetPrefabEntity {
            get {
                if (m_SelectedNetLanePrefabEntity != Entity.Null) {
                    return m_SelectedNetLanePrefabEntity;
                }

                return m_SelectedNetPrefabEntity;
            }
        }

        public override string toolID => "NT_BaseToolSystem";

        /// <summary>
        ///     Returns hint tooltip entries for the current operation phase.
        ///     Override in derived tools to provide tool-specific tooltips.
        /// </summary>
        /// <param name="phase">The current operation phase.</param>
        /// <param name="applyAction">The primary apply action (left click).</param>
        /// <param name="secondaryApplyAction">The secondary apply action (right click).</param>
        /// <returns>A list of tooltip entries for the given phase, or empty if none.</returns>
        public virtual IReadOnlyList<HintTooltipEntry> GetHintTooltips(
            OperationPhase phase,
            ProxyAction    applyAction,
            ProxyAction    secondaryApplyAction) {
            return System.Array.Empty<HintTooltipEntry>();
        }

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
            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
            m_RenderingSystem     = World.GetOrCreateSystemManaged<RenderingSystem>();

            // Move this tool to the front of the tool stack so it takes priority over vanilla tools
            m_ToolSystem.tools.Remove(this);
            m_ToolSystem.tools.Insert(0, this);

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
            m_UnselectedNodesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                                                             .WithAll<Node>()
                                                             .WithNone<NT_Eligible, NT_Selected>()
                                                             .Build();
            m_NodesWithEligibleQuery = SystemAPI.QueryBuilder()
                                                .WithAll<Node, NT_Eligible>()
                                                .Build();
            m_NodesWithHighlightedQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Node>()
                                                   .WithAny<Highlighted, NT_Highlighted>()
                                                   .Build();
            m_EntitiesWithHighlightedQuery = SystemAPI.QueryBuilder()
                                                      .WithAny<Highlighted, NT_Highlighted>()
                                                      .Build();
            m_NodesWithSelectedQuery = SystemAPI.QueryBuilder()
                                                .WithAll<Node, NT_Selected>()
                                                .Build();
            m_AllNtComponentsQuery = SystemAPI.QueryBuilder()
                                              .WithAny<NT_Eligible, NT_Highlighted, NT_Selected, NT_SelectedFirst,
                                                  NT_SelectedLast>()
                                              .Build();
            m_NodesWithSelectedFirstQuery = SystemAPI.QueryBuilder()
                                                     .WithAll<Node, NT_SelectedFirst>()
                                                     .Build();
            m_NodesWithSelectedLastQuery = SystemAPI.QueryBuilder()
                                                    .WithAll<Node, NT_SelectedLast>()
                                                    .Build();
            m_EdgesWithHighlightedQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Edge>()
                                                   .WithAny<Highlighted, NT_Highlighted>()
                                                   .Build();
            m_EdgesWithSelectedQuery = SystemAPI.QueryBuilder()
                                                .WithAll<Edge, NT_Selected>()
                                                .Build();

            // Per-target-flag queries for MarkEligibleNodes
            m_TargetRoadNodesQuery = SystemAPI.QueryBuilder()
                                              .WithAll<Node, Road>()
                                              .WithNone<NT_Eligible>()
                                              .Build();
            m_TargetPathNodesQuery = SystemAPI.QueryBuilder()
                                              .WithAll<Node, LocalConnect>()
                                              .WithNone<Marker>()
                                              .WithNone<NT_Eligible>()
                                              .Build();
            m_TargetInvisiblePathNodesQuery = SystemAPI.QueryBuilder()
                                                       .WithAll<Node, LocalConnect>()
                                                       .WithAll<Marker>()
                                                       .WithNone<NT_Eligible>()
                                                       .Build();
            m_TargetRailNodesQuery = SystemAPI.QueryBuilder()
                                              .WithAll<Node>()
                                              .WithAny<TrainTrack, TramTrack, SubwayTrack>()
                                              .WithNone<NT_Eligible>()
                                              .Build();
            m_TargetWaterwayNodesQuery = SystemAPI.QueryBuilder()
                                                  .WithAll<Node, Waterway>()
                                                  .WithNone<NT_Eligible>()
                                                  .Build();

            // Per-target-flag queries for MarkEligibleEdges
            m_EdgesWithEligibleQuery = SystemAPI.QueryBuilder()
                                                .WithAll<Edge, NT_Eligible>()
                                                .Build();
            m_EdgesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Edge>()
                                                   .WithNone<NT_Eligible>()
                                                   .Build();
            m_TargetRoadEdgesQuery = SystemAPI.QueryBuilder()
                                              .WithAll<Edge, Road>()
                                              .WithNone<NT_Eligible>()
                                              .Build();
            m_TargetPathEdgesQuery = SystemAPI.QueryBuilder()
                                              .WithAll<Edge, LocalConnect>()
                                              .WithNone<Marker>()
                                              .WithNone<NT_Eligible>()
                                              .Build();
            m_TargetInvisiblePathEdgesQuery = SystemAPI.QueryBuilder()
                                                       .WithAll<Edge, LocalConnect>()
                                                       .WithAll<Marker>()
                                                       .WithNone<NT_Eligible>()
                                                       .Build();
            m_TargetRailEdgesQuery = SystemAPI.QueryBuilder()
                                              .WithAll<Edge>()
                                              .WithAny<TrainTrack, TramTrack, SubwayTrack>()
                                              .WithNone<NT_Eligible>()
                                              .Build();
            m_TargetWaterwayEdgesQuery = SystemAPI.QueryBuilder()
                                                  .WithAll<Edge, Waterway>()
                                                  .WithNone<NT_Eligible>()
                                                  .Build();

            // Vanilla container query
            m_ContainerQuery = GetContainerQuery();
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

        /// <summary>
        ///     Default prefab activation logic driven by <see cref="IToolPrefabProvider" />
        ///     and <see cref="INetPrefabCachingProvider" />.
        ///     Tools with custom activation logic (e.g. multi-component checks) may override.
        /// </summary>
        public override bool TrySetPrefab(PrefabBase prefab) {
            // Delegate to net prefab caching if this tool supports it
            if (this is INetPrefabCachingProvider) {
                var result = TryCacheNetPrefab(prefab);
                if (result.HasValue) {
                    return result.Value;
                }
            }

            // Standard tool activation via IToolPrefabProvider
            if (this is IToolPrefabProvider toolProvider) {
                m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {toolProvider.HasToolComponent(prefab)}");

                var validRequest = prefab is NT_ToolPrefab && toolProvider.HasToolComponent(prefab);
                if (!validRequest) {
                    return false;
                }

                m_Prefab = prefab;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Attempts to cache a net prefab selection.
        /// </summary>
        /// <param name="prefab">The prefab offered by the game.</param>
        /// <returns>
        ///     <c>true</c> to consume the prefab (tool is active and should reflect the change),
        ///     <c>false</c> to reject (prefab was cached but tool is not active),
        ///     or <c>null</c> to fall through to the standard tool-activation check.
        /// </returns>
        public bool? TryCacheNetPrefab(PrefabBase prefab) {
            switch (prefab) {
                case RoadPrefab or TrackPrefab or WaterwayPrefab or PathwayPrefab:
                {
                    m_SelectedNetPrefab           = (NetPrefab)prefab;
                    m_SelectedNetPrefabEntity     = m_PrefabSystem.GetEntity(m_SelectedNetPrefab);
                    m_SelectedNetLanePrefab       = null;
                    m_SelectedNetLanePrefabEntity = Entity.Null;
                    return m_ToolSystem.activeTool == this;
                }
                case NetLanePrefab netLanePrefab:
                {
                    m_SelectedNetLanePrefab       = netLanePrefab;
                    m_SelectedNetLanePrefabEntity = m_PrefabSystem.GetEntity(netLanePrefab);
                    var foundContainers = GetContainers(m_ContainerQuery, out var laneContainer, out _);
                    m_SelectedNetPrefab       = null;
                    m_SelectedNetPrefabEntity = laneContainer;
                    return m_ToolSystem.activeTool == this;
                }
                default:
                    return null;
            }
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

            // Restore persisted preferences, masked by what this tool supports
            var settings    = NetworkToolsMod.Instance?.Settings;
            SelectedSnaps   = settings != null ? (SnapOption)settings.SavedSelectedSnaps & AvailableSnaps : AvailableSnaps;
            SelectedTargets = settings != null ? (TargetOption)settings.SavedSelectedTargets & AvailableTargets : AvailableTargets;
            SelectedViews   = settings != null ? (ViewOption)settings.SavedSelectedViews & AvailableViews : ViewOption.None;

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

            // Cleanup rendering state
            m_RenderingSystem.markersVisible = false;

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
            EntityManager.RemoveComponent<NT_Eligible>(m_AllNtComponentsQuery);

            // Add BatchesUpdated BEFORE removing components, because the query won't match after removal
            EntityManager.AddComponent<BatchesUpdated>(m_EntitiesWithHighlightedQuery);
            EntityManager.RemoveComponent(m_EntitiesWithHighlightedQuery, HighlightedComponentTypeSet);
        }

        /// <summary>
        ///     Updates action enabled states. Override to customize action behavior.
        /// </summary>
        protected virtual void UpdateActions() {
            m_ApplyAction.shouldBeEnabled          = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
        }


        /// <summary>
        ///     Swaps highlighting between two entities (removes from old, adds to new).
        ///     Simple single-node highlighting utility.
        /// </summary>
        /// <param name="oldEntity">Entity to remove highlighting from</param>
        /// <param name="newEntity">Entity to add highlighting to</param>
        protected virtual void SwapHighlitedEntities(Entity oldEntity, Entity newEntity, NT_Highlighted highlightData) {
            RemoveHighlight(oldEntity);
            AddHighlight(newEntity, highlightData);
        }

        /// <summary>
        ///     Adds NT_Highlighted component to an entity.
        /// </summary>
        protected virtual void AddHighlight(Entity entity, NT_Highlighted highlightData) {
            if (entity == Entity.Null) {
                return;
            }

            EntityManager.AddComponentData(entity, highlightData);
            EntityManager.AddComponent<Highlighted>(entity);

            if (!EntityManager.HasComponent<BatchesUpdated>(entity)) {
                EntityManager.AddComponent<BatchesUpdated>(entity);
            }
        }

        /// <summary>
        ///     Removes NT_Highlighted component from an entity.
        /// </summary>
        protected virtual void RemoveHighlight(Entity entity) {
            if (entity == Entity.Null) {
                return;
            }

            EntityManager.RemoveComponent(entity, HighlightedComponentTypeSet);

            if (!EntityManager.HasComponent<BatchesUpdated>(entity)) {
                EntityManager.AddComponent<BatchesUpdated>(entity);
            }
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
            // Add BatchesUpdated BEFORE removing components, because the query won't match after removal
            EntityManager.AddComponent<BatchesUpdated>(m_EntitiesWithHighlightedQuery);
            EntityManager.RemoveComponent(m_EntitiesWithHighlightedQuery, HighlightedComponentTypeSet);
        }

        /// <summary>
        ///     Adds NT_Eligible component to entities matching the current target flags.
        ///     Marks nodes or edges depending on <see cref="EligibilityTarget" />.
        ///     When <see cref="UseCustomEligibilityFilter" /> is true, also applies
        ///     <see cref="FilterEligibleEntity" /> per entity.
        /// </summary>
        protected void MarkEligibleEntities() {
            var targets = SelectedTargets & AvailableTargets;

            if (!UseCustomEligibilityFilter) {
                AddEligibleByTargets(targets);
            } else {
                AddEligibleByTargetsFiltered(targets);
            }
        }

        /// <summary>
        ///     Removes all current eligibility and re-applies based on current target flags.
        ///     Called by the UI when the player toggles target options.
        ///     Invokes <see cref="OnEligibilityReset" /> between stripping and re-marking
        ///     so derived tools can clean up phase-specific state.
        /// </summary>
        public void RefreshEligibility() {
            EntityManager.RemoveComponent<NT_Eligible>(m_AllNtComponentsQuery);
            OnEligibilityReset();
            MarkEligibleEntities();
        }

        /// <summary>
        ///     Refreshes views
        /// </summary>
        public void RefreshViews() {
            requireUnderground               = (SelectedViews & ViewOption.Underground)       != 0;
            requireZones                     = (SelectedViews & ViewOption.ZoneGrid)          != 0;
            m_RenderingSystem.markersVisible = (SelectedViews & ViewOption.InvisibleNetworks) != 0;
        }

        /// <summary>
        ///     Called during <see cref="RefreshEligibility" /> after eligibility is stripped
        ///     but before it is re-applied. Override to reset tool-specific state
        ///     (selections, handles, phase) when the player changes target options.
        /// </summary>
        protected virtual void OnEligibilityReset() {
        }

        /// <summary>
        ///     Per-entity eligibility filter called when <see cref="UseCustomEligibilityFilter" /> is true.
        ///     Override in derived tools to apply tool-specific criteria.
        /// </summary>
        /// <param name="entity">Candidate node entity that already matches target flags.</param>
        /// <returns>True if the entity should be marked eligible.</returns>
        protected virtual bool FilterEligibleEntity(Entity entity) {
            return true;
        }

        /// <summary>
        ///     Fast path: batch-adds NT_Eligible via static queries without per-entity filtering.
        /// </summary>
        private void AddEligibleByTargets(TargetOption targets) {
            if (EligibilityTarget == EligibilityTarget.Edge) {
                AddEligibleEdgesByTargets(targets);
            } else {
                AddEligibleNodesByTargets(targets);
            }
        }

        /// <summary>
        ///     Fast path: batch-adds NT_Eligible to nodes via static queries.
        /// </summary>
        private void AddEligibleNodesByTargets(TargetOption targets) {
            if ((targets & TargetOption.All) == TargetOption.All) {
                EntityManager.AddComponent<NT_Eligible>(m_NodesWithoutEligibleQuery);
                return;
            }

            if ((targets & TargetOption.Road) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetRoadNodesQuery);
            }

            if ((targets & TargetOption.Path) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetPathNodesQuery);
            }

            if ((targets & TargetOption.Rail) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetRailNodesQuery);
            }

            if ((targets & TargetOption.Waterway) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetWaterwayNodesQuery);
            }

            if ((targets & TargetOption.InvisiblePath) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetInvisiblePathNodesQuery);
            }
        }

        /// <summary>
        ///     Fast path: batch-adds NT_Eligible to edges via static queries.
        /// </summary>
        private void AddEligibleEdgesByTargets(TargetOption targets) {
            if ((targets & TargetOption.All) == TargetOption.All) {
                EntityManager.AddComponent<NT_Eligible>(m_EdgesWithoutEligibleQuery);
                return;
            }

            if ((targets & TargetOption.Road) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetRoadEdgesQuery);
            }

            if ((targets & TargetOption.Path) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetPathEdgesQuery);
            }

            if ((targets & TargetOption.Rail) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetRailEdgesQuery);
            }

            if ((targets & TargetOption.Waterway) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetWaterwayEdgesQuery);
            }

            if ((targets & TargetOption.InvisiblePath) != 0) {
                EntityManager.AddComponent<NT_Eligible>(m_TargetInvisiblePathEdgesQuery);
            }
        }

        /// <summary>
        ///     Slow path: iterates candidate entities and applies <see cref="FilterEligibleEntity" /> per entity.
        /// </summary>
        private void AddEligibleByTargetsFiltered(TargetOption targets) {
            if (EligibilityTarget == EligibilityTarget.Edge) {
                AddEligibleEdgesByTargetsFiltered(targets);
            } else {
                AddEligibleNodesByTargetsFiltered(targets);
            }
        }

        /// <summary>
        ///     Slow path: iterates candidate nodes and applies <see cref="FilterEligibleEntity" /> per entity.
        /// </summary>
        private void AddEligibleNodesByTargetsFiltered(TargetOption targets) {
            if ((targets & TargetOption.All) == TargetOption.All) {
                FilterAndAddEligible(m_NodesWithoutEligibleQuery);
                return;
            }

            if ((targets & TargetOption.Road) != 0) {
                FilterAndAddEligible(m_TargetRoadNodesQuery);
            }

            if ((targets & TargetOption.Path) != 0) {
                FilterAndAddEligible(m_TargetPathNodesQuery);
            }

            if ((targets & TargetOption.Rail) != 0) {
                FilterAndAddEligible(m_TargetRailNodesQuery);
            }

            if ((targets & TargetOption.Waterway) != 0) {
                FilterAndAddEligible(m_TargetWaterwayNodesQuery);
            }

            if ((targets & TargetOption.InvisiblePath) != 0) {
                FilterAndAddEligible(m_TargetInvisiblePathNodesQuery);
            }
        }

        /// <summary>
        ///     Slow path: iterates candidate edges and applies <see cref="FilterEligibleEntity" /> per entity.
        /// </summary>
        private void AddEligibleEdgesByTargetsFiltered(TargetOption targets) {
            if ((targets & TargetOption.All) == TargetOption.All) {
                FilterAndAddEligible(m_EdgesWithoutEligibleQuery);
                return;
            }

            if ((targets & TargetOption.Road) != 0) {
                FilterAndAddEligible(m_TargetRoadEdgesQuery);
            }

            if ((targets & TargetOption.Path) != 0) {
                FilterAndAddEligible(m_TargetPathEdgesQuery);
            }

            if ((targets & TargetOption.Rail) != 0) {
                FilterAndAddEligible(m_TargetRailEdgesQuery);
            }

            if ((targets & TargetOption.Waterway) != 0) {
                FilterAndAddEligible(m_TargetWaterwayEdgesQuery);
            }

            if ((targets & TargetOption.InvisiblePath) != 0) {
                FilterAndAddEligible(m_TargetInvisiblePathEdgesQuery);
            }
        }

        /// <summary>
        ///     Iterates entities from a query and adds NT_Eligible to those passing the custom filter.
        /// </summary>
        private void FilterAndAddEligible(EntityQuery query) {
            var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities) {
                if (FilterEligibleEntity(entity)) {
                    EntityManager.AddComponent<NT_Eligible>(entity);
                }
            }

            entities.Dispose();
        }
    }
}