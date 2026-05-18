namespace NetworkTools.Systems.Tools {
    using System.Collections.Generic;

    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Handles;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    ///     Tracks the current handle input interaction state.
    /// </summary>
    public enum HandleInputState {
        /// <summary>Not pressing anything, can hover over handles.</summary>
        Idle = 0,

        /// <summary>Mouse down on a handle, waiting to determine if click or drag.</summary>
        PendingAction = 1,

        /// <summary>Confirmed drag in progress on a handle.</summary>
        Dragging = 2
    }

    /// <summary>
    ///     Partial class containing centralized handle management for all tool systems.
    /// </summary>
    public abstract partial class NT_BaseToolSystem {
        /// <summary>
        ///     Override to return true when the tool should perform handle raycasting.
        ///     Default returns true when handles exist.
        /// </summary>
        protected virtual bool ShouldRaycastHandles => m_Handles.IsCreated && m_Handles.Length > 0;

        /// <summary>
        ///     World units the mouse must move before being considered a drag.
        /// </summary>
        protected const float HandleDragThreshold = 0.5f;

        /// <summary>
        ///     List of all handle entities created by this tool.
        /// </summary>
        protected NativeList<Entity> m_Handles;

        /// <summary>
        ///     Current handle input state.
        /// </summary>
        protected HandleInputState m_HandleInputState;

        /// <summary>
        ///     The handle entity currently being dragged, or Entity.Null.
        /// </summary>
        protected Entity m_DraggedHandle;

        /// <summary>
        ///     World position when mouse was pressed on a handle (for drag threshold detection).
        /// </summary>
        protected float3 m_HandleMouseDownPosition;

        /// <summary>
        ///     Angular offset between the mouse's initial position on the circle
        ///     and the handle's angle at drag start, so dragging is relative.
        /// </summary>
        private float m_RotationDragOffset;

        /// <summary>
        ///     Entity query for all handle entities.
        /// </summary>
        protected EntityQuery m_HandleQuery;

        /// <summary>
        ///     Initializes handle management. Called from OnCreate().
        /// </summary>
        protected void InitializeHandles() {
            m_Handles          = new NativeList<Entity>(16, Allocator.Persistent);
            m_HandleInputState = HandleInputState.Idle;
            m_DraggedHandle    = Entity.Null;

            m_HandleQuery = SystemAPI.QueryBuilder()
                                     .WithAll<NT_Handle, NT_HandlePosition, NT_HandleLink>()
                                     .Build();
        }

        #region Spec-Driven Dispatch

        /// <summary>
        ///     Returns the mode flag for the tool's current mode.
        ///     Override in concrete tools that use an enum mode parameter.
        /// </summary>
        protected virtual int GetActiveModeFlag() => 0;

        /// <summary>
        ///     Returns true when the parameter is visible in the given mode.
        ///     A parameter with Modes == 0 is visible in all modes.
        /// </summary>
        private static bool IsModeVisible(int paramModes, int activeMode) {
            return paramModes == 0 || (paramModes & activeMode) != 0;
        }

        /// <summary>
        ///     Walks <see cref="Parameters"/>.<see cref="ParameterBase.Handles"/>,
        ///     creates ECS handle entities for the active mode, and populates
        ///     <see cref="m_HandleEntries"/>, <see cref="m_ParameterHandles"/>, and <see cref="m_ParentChildLinks"/>.
        /// </summary>
        protected void RebuildHandlesForActiveMode() {
            CancelHandleInteraction();
            DestroyAllHandles();

            var active = GetActiveModeFlag();
            foreach (var param in Parameters) {
                var specs = GetHandleSpecs(param);
                if (specs == null) continue;
                if (!IsModeVisible(param.Modes, active)) continue;

                foreach (IHandleSpec spec in specs) {
                    var pos    = ResolveInitialPosition(param, spec);
                    var entity = CreateHandleFromSpec(spec, pos, param);
                    m_HandleEntries[entity] = new HandleEntry(param, spec);

                    if (!m_ParameterHandles.TryGetValue(param, out var list)) {
                        list = new List<Entity>(2);
                        m_ParameterHandles[param] = list;
                    }
                    list.Add(entity);
                }
            }

            ResolveParentLinks();
            BuildParentChildLinks();
        }

        /// <summary>
        ///     Extracts the <see cref="IHandleSpec"/> array from a parameter, regardless of its generic type.
        /// </summary>
        private static IHandleSpec[] GetHandleSpecs(ParameterBase param) {
            return param switch {
                Float3Parameter f3p => f3p.Handles,
                FloatParameter  fp  => fp.Handles,
                _                   => null
            };
        }

        /// <summary>
        ///     Computes the initial world position for a handle being created.
        /// </summary>
        private float3 ResolveInitialPosition(ParameterBase param, IHandleSpec spec) {
            switch (param) {
                case Float3Parameter f3p: {
                    var s = (IHandleSpec<float3>)spec;
                    return s.ComputePosition != null ? s.ComputePosition(this, f3p.Value) : f3p.Value;
                }
                case FloatParameter fp: {
                    var s = (IHandleSpec<float>)spec;
                    if (s.ComputePosition != null) return s.ComputePosition(this, fp.Value);

                    // Circle/rotation handles position at the parent; position will be set after parent resolution
                    return float3.zero;
                }
                default:
                    return float3.zero;
            }
        }

        /// <summary>
        ///     Creates a single ECS handle entity from a spec, dispatching by type.
        /// </summary>
        private Entity CreateHandleFromSpec(IHandleSpec spec, float3 position, ParameterBase param) {
            var typeFlags = spec.TypeFlags;
            var radius    = spec.Size > 0f ? spec.Size : NT_Handle.SizePrimary;

            if ((typeFlags & HandleTypeFlags.Rotation) != 0 && spec is RotationHandle rot) {
                var normal = math.lengthsq(rot.Normal) > 0f
                    ? math.normalizesafe(rot.Normal)
                    : new float3(0f, 1f, 0f);
                var refDir = math.lengthsq(rot.ReferenceDirection) > 0f
                    ? math.normalizesafe(rot.ReferenceDirection)
                    : new float3(1f, 0f, 0f);

                float angle = 0f;
                if (param is Float3Parameter f3p) {
                    var direction = f3p.Value;
                    if (math.lengthsq(direction) > 0.0001f) {
                        var perp = math.cross(normal, refDir);
                        angle = math.atan2(math.dot(direction, perp), math.dot(direction, refDir));
                    }
                }

                return CreateRotationHandle(Entity.Null, 0, position, radius, normal, refDir, angle, typeFlags);
            }

            if ((typeFlags & HandleTypeFlags.Circle) != 0 && spec is CircleHandle circ) {
                var circleRadius = param is FloatParameter fp ? fp.Value : 0f;
                var normal = math.lengthsq(circ.Normal) > 0f
                    ? math.normalizesafe(circ.Normal)
                    : new float3(0f, 1f, 0f);
                return CreateCircleHandle(Entity.Null, 0, position, circleRadius, normal, typeFlags);
            }

            // Axis handle: compute constraints dynamically from endpoint delegates
            if (spec is AxisHandle axis) {
                axis.GetAxisInfo(this, out var origin, out var axisDir, out var pathLen);
                var constraints = param is FloatParameter axFp
                    ? NT_HandleConstraints.AxisWithBounds(axisDir, origin, axFp.Min * pathLen, axFp.Max * pathLen)
                    : NT_HandleConstraints.AxisOnly(axisDir, origin);
                return CreatePositionHandle(Entity.Null, Entity.Null, 0, position, typeFlags, constraints, radius);
            }

            // Position or ComputedPosition
            return CreatePositionHandle(Entity.Null, Entity.Null, 0, position, typeFlags, spec.Constraints, radius);
        }

        /// <summary>
        ///     Resolves <see cref="IHandleSpec.Parent"/>, <c>ReferenceDirectionFrom</c>, and <c>NormalFrom</c>
        ///     name references to parameter values and entities.
        /// </summary>
        private void ResolveParentLinks() {
            var nameToParam = new Dictionary<string, ParameterBase>();
            foreach (var field in GetType().GetFields(
                         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)) {
                if (typeof(ParameterBase).IsAssignableFrom(field.FieldType)) {
                    nameToParam[field.Name] = (ParameterBase)field.GetValue(this);
                }
            }

            foreach (var (entity, entry) in m_HandleEntries) {
                // Resolve dynamic field references on typed handles
                if (entry.Spec is RotationHandle rot) {
                    ResolveRotationFields(entity, rot, nameToParam);
                } else if (entry.Spec is CircleHandle circ) {
                    ResolveCircleFields(entity, circ, nameToParam);
                } else if (entry.Spec is PositionHandle ph) {
                    ResolvePositionConstraintFields(entity, ph, nameToParam);
                }

                // Resolve Parent
                var parentName = entry.Spec.Parent;
                if (string.IsNullOrEmpty(parentName)) continue;

                if (!nameToParam.TryGetValue(parentName, out var parentParam)) continue;
                if (parentParam is not Float3Parameter parentF3) continue;

                switch (entry.Spec) {
                    case PositionHandle ph:        ph.ResolvedParent = parentF3; break;
                    case CircleHandle ch:          ch.ResolvedParent = parentF3; break;
                    case RotationHandle rh:        rh.ResolvedParent = parentF3; break;
                    case ComputedPositionHandle c: c.ResolvedParent  = parentF3; break;
                    case AxisHandle ax:            ax.ResolvedParent = parentF3; break;
                }

                if (m_ParameterHandles.TryGetValue(parentParam, out var parentEntities) && parentEntities.Count > 0) {
                    var parentEntity = parentEntities[0];
                    EntityManager.AddComponentData(entity, new NT_HandleParent { Parent = parentEntity });

                    if (entry.Spec is CircleHandle || entry.Spec is RotationHandle) {
                        EntityManager.SetComponentData(entity,
                            new NT_HandlePosition { Position = parentF3.Value });
                    }
                }
            }
        }

        /// <summary>
        ///     Resolves <c>ReferenceDirectionFrom</c> and <c>NormalFrom</c> on a rotation handle.
        ///     Reads the referenced parameter's current value and updates the ECS components.
        /// </summary>
        private void ResolveRotationFields(Entity entity, RotationHandle rot, Dictionary<string, ParameterBase> nameToParam) {
            var refDir = rot.ReferenceDirection;
            var normal = rot.Normal;

            if (!string.IsNullOrEmpty(rot.ReferenceDirectionFrom)
                && nameToParam.TryGetValue(rot.ReferenceDirectionFrom, out var refParam)
                && refParam is Float3Parameter refF3) {
                var v = refF3.Value;
                if (math.lengthsq(v) > 0.0001f) refDir = math.normalizesafe(v);
            }

            if (!string.IsNullOrEmpty(rot.NormalFrom)
                && nameToParam.TryGetValue(rot.NormalFrom, out var normParam)
                && normParam is Float3Parameter normF3) {
                var v = normF3.Value;
                if (math.lengthsq(v) > 0.0001f) normal = math.normalizesafe(v);
            }

            refDir = math.lengthsq(refDir) > 0f ? math.normalizesafe(refDir) : new float3(1f, 0f, 0f);
            normal = math.lengthsq(normal) > 0f ? math.normalizesafe(normal) : new float3(0f, 1f, 0f);

            // Recompute initial angle from the owning parameter's current value
            float angle = 0f;
            if (m_HandleEntries.TryGetValue(entity, out var he) && he.Parameter is Float3Parameter f3p) {
                var direction = f3p.Value;
                if (math.lengthsq(direction) > 0.0001f) {
                    var perp = math.cross(normal, refDir);
                    angle = math.atan2(math.dot(direction, perp), math.dot(direction, refDir));
                }
            }

            // Update ECS component with resolved values
            var rotData = EntityManager.GetComponentData<NT_HandleRotation>(entity);
            rotData.Normal             = normal;
            rotData.ReferenceDirection = refDir;
            rotData.Angle              = angle;
            EntityManager.SetComponentData(entity, rotData);
        }

        /// <summary>
        ///     Resolves <c>NormalFrom</c> on a circle handle.
        /// </summary>
        private void ResolveCircleFields(Entity entity, CircleHandle circ, Dictionary<string, ParameterBase> nameToParam) {
            if (string.IsNullOrEmpty(circ.NormalFrom)) return;

            if (nameToParam.TryGetValue(circ.NormalFrom, out var normParam)
                && normParam is Float3Parameter normF3) {
                var v = normF3.Value;
                if (math.lengthsq(v) > 0.0001f && EntityManager.HasComponent<NT_HandleCircle>(entity)) {
                    var circle = EntityManager.GetComponentData<NT_HandleCircle>(entity);
                    circle.Normal = math.normalizesafe(v);
                    EntityManager.SetComponentData(entity, circle);
                }
            }
        }

        /// <summary>
        ///     Resolves <c>ConstraintAxisFrom</c> and <c>ConstraintOriginFrom</c> on a position handle.
        ///     When both are set, builds an axis constraint from the referenced parameters and
        ///     applies it to the ECS entity.
        /// </summary>
        private void ResolvePositionConstraintFields(Entity entity, PositionHandle ph, Dictionary<string, ParameterBase> nameToParam) {
            ph.ResolvedConstraintAxis   = null;
            ph.ResolvedConstraintOrigin = null;

            if (!string.IsNullOrEmpty(ph.ConstraintAxisFrom)
                && nameToParam.TryGetValue(ph.ConstraintAxisFrom, out var axisParam)
                && axisParam is Float3Parameter axisF3) {
                ph.ResolvedConstraintAxis = axisF3;
            }

            if (!string.IsNullOrEmpty(ph.ConstraintOriginFrom)
                && nameToParam.TryGetValue(ph.ConstraintOriginFrom, out var originParam)
                && originParam is Float3Parameter originF3) {
                ph.ResolvedConstraintOrigin = originF3;
            }

            if (ph.ResolvedConstraintAxis == null || ph.ResolvedConstraintOrigin == null) return;

            var axis   = ph.ResolvedConstraintAxis.Value;
            var origin = ph.ResolvedConstraintOrigin.Value;
            if (math.lengthsq(axis) < 0.0001f) return;

            var constraints = NT_HandleConstraints.AxisOnly(axis, origin);
            if (EntityManager.HasComponent<NT_HandleConstraints>(entity)) {
                EntityManager.SetComponentData(entity, constraints);
            } else {
                EntityManager.AddComponentData(entity, constraints);
            }
        }

        /// <summary>
        ///     Populates <see cref="m_ParentChildLinks"/> from resolved parent references.
        ///     Each Float3Parameter that is referenced as a Parent gets an entry mapping to its child Float3Parameters.
        /// </summary>
        private void BuildParentChildLinks() {
            m_ParentChildLinks.Clear();
            var parentToChildren = new Dictionary<Float3Parameter, List<ParentChildLink>>();

            foreach (var (_, entry) in m_HandleEntries) {
                var parentName = entry.Spec.Parent;
                if (string.IsNullOrEmpty(parentName)) continue;
                if (entry.Parameter is not Float3Parameter childF3) continue;

                // Rotation handles store a direction, not a position — shifting by the
                // parent's position delta would corrupt their value.  Their entity center
                // is already repositioned by SyncParentPositionToChildHandles.
                if (entry.Spec is RotationHandle) continue;

                // Find the resolved parent from the spec
                Float3Parameter parentF3 = entry.Spec switch {
                    PositionHandle ph        => ph.ResolvedParent,
                    CircleHandle ch          => ch.ResolvedParent,
                    ComputedPositionHandle c => c.ResolvedParent,
                    AxisHandle ax            => ax.ResolvedParent,
                    _                        => null
                };

                if (parentF3 == null) continue;

                if (!parentToChildren.TryGetValue(parentF3, out var list)) {
                    list = new List<ParentChildLink>();
                    parentToChildren[parentF3] = list;
                }

                // Avoid duplicate entries for the same child
                var alreadyLinked = false;
                foreach (var existing in list) {
                    if (existing.Child == childF3) { alreadyLinked = true; break; }
                }
                if (!alreadyLinked) {
                    list.Add(new ParentChildLink { Child = childF3, LastParentPos = parentF3.Value });
                }
            }

            foreach (var (parent, list) in parentToChildren) {
                m_ParentChildLinks[parent] = list.ToArray();
            }
        }

        /// <summary>
        ///     Generic drag dispatch for spec-driven handles.
        ///     Routes to the correct <see cref="Parameter{T}.SetValue"/> via <see cref="IHandleSpec{T}.ComputeFromPosition"/>.
        /// </summary>
        private void DispatchDrag(Entity handle, float3 position) {
            if (!m_HandleEntries.TryGetValue(handle, out var entry)) return;

            switch (entry.Parameter) {
                case Float3Parameter f3p: {
                    var s = (IHandleSpec<float3>)entry.Spec;
                    var v = s.ComputeFromPosition != null ? s.ComputeFromPosition(this, position) : position;
                    f3p.SetValue(v, ChangeOrigin.Handle);
                    break;
                }
                case FloatParameter fp: {
                    var s = (IHandleSpec<float>)entry.Spec;
                    System.Diagnostics.Debug.Assert(s.ComputeFromPosition != null,
                        $"FloatParameter handle for '{fp.Key}' has no ComputeFromPosition delegate");
                    fp.SetValue(s.ComputeFromPosition(this, position), ChangeOrigin.Handle);
                    break;
                }
            }
        }

        /// <summary>
        ///     Dispatch for circle handle drag (radius change) using spec-driven path.
        /// </summary>
        private void DispatchCircleDrag(Entity handle, float radius) {
            if (!m_HandleEntries.TryGetValue(handle, out var entry)) return;

            if (entry.Parameter is FloatParameter fp) {
                fp.SetValue(radius, ChangeOrigin.Handle);
            }
        }

        /// <summary>
        ///     Dispatch for rotation handle drag using spec-driven path.
        /// </summary>
        private void DispatchRotationDrag(Entity handle, float angle, float3 direction) {
            if (!m_HandleEntries.TryGetValue(handle, out var entry)) return;

            if (entry.Parameter is Float3Parameter f3p) {
                var s = (IHandleSpec<float3>)entry.Spec;
                var v = s.ComputeFromPosition != null ? s.ComputeFromPosition(this, direction) : direction;
                f3p.SetValue(v, ChangeOrigin.Handle);
            }
        }

        /// <summary>
        ///     When a Float3Parameter that is a parent changes, update the center position
        ///     on any child circle or rotation handle entities that reference it.
        /// </summary>
        private void SyncParentPositionToChildHandles(Float3Parameter parent) {
            if (m_HandleEntries == null || m_HandleEntries.Count == 0) return;

            var pos = parent.Value;
            foreach (var (entity, entry) in m_HandleEntries) {
                Float3Parameter resolvedParent = entry.Spec switch {
                    CircleHandle ch         => ch.ResolvedParent,
                    RotationHandle rh       => rh.ResolvedParent,
                    _                       => null
                };
                if (resolvedParent != parent) continue;
                if (!EntityManager.Exists(entity)) continue;

                EntityManager.SetComponentData(entity,
                    new NT_HandlePosition { Position = pos });
            }
        }

        #endregion

        /// <summary>
        ///     Disposes handle management resources. Called from OnDestroy().
        /// </summary>
        protected void DisposeHandles() {
            DestroyAllHandles();
            if (m_Handles.IsCreated) m_Handles.Dispose();
        }

        /// <summary>
        ///     Cleans up handles when tool stops running. Called from OnStopRunning().
        /// </summary>
        protected void CleanupHandles() {
            DestroyAllHandles();
            m_HandleInputState = HandleInputState.Idle;
            m_DraggedHandle    = Entity.Null;
        }

        #region Handle Creation

        /// <summary>
        ///     Creates a position handle for controlling a world position.
        /// </summary>
        /// <param name="linkedEntity">The primary entity this handle controls.</param>
        /// <param name="linkedEdge">Optional edge entity for bezier handles.</param>
        /// <param name="key">Identifier key (e.g., bezier point index).</param>
        /// <param name="position">Initial world position of the handle.</param>
        /// <param name="typeFlags">Type flags defining the handle's purpose.</param>
        /// <param name="constraints">Optional movement constraints.</param>
        /// <param name="radius">Hit detection and visual radius.</param>
        /// <returns>The created handle entity.</returns>
        protected Entity CreatePositionHandle(
            Entity                linkedEntity,
            Entity                linkedEdge,
            int                   key,
            float3                position,
            HandleTypeFlags       typeFlags,
            NT_HandleConstraints? constraints = null,
            float                 radius = NT_Handle.SizePrimary) {
            var handle = EntityManager.CreateEntity();

            EntityManager.AddComponentData(handle, NT_Handle.Create(typeFlags | HandleTypeFlags.Position, radius));
            EntityManager.AddComponentData(handle,
                                           new NT_HandleLink {
                                               LinkedEntity = linkedEntity,
                                               LinkedEdge   = linkedEdge,
                                               Key          = key
                                           });
            EntityManager.AddComponentData(handle,
                                           new NT_HandlePosition { Position = position });

            if (constraints.HasValue) {
                EntityManager.AddComponentData(handle, constraints.Value);
            }

            m_Handles.Add(handle);
            return handle;
        }

        /// <summary>
        ///     Creates a line handle representing two connected points.
        /// </summary>
        /// <param name="linkedEntity">The entity this handle controls.</param>
        /// <param name="key">Identifier key.</param>
        /// <param name="pointA">First endpoint of the line.</param>
        /// <param name="pointB">Second endpoint of the line.</param>
        /// <param name="typeFlags">Type flags defining the handle's purpose.</param>
        /// <returns>The created handle entity.</returns>
        protected Entity CreateLineHandle(
            Entity          linkedEntity,
            int             key,
            float3          pointA,
            float3          pointB,
            HandleTypeFlags typeFlags) {
            var handle = EntityManager.CreateEntity();

            EntityManager.AddComponentData(handle, NT_Handle.Create(typeFlags | HandleTypeFlags.Line));
            EntityManager.AddComponentData(handle,
                                           new NT_HandleLink {
                                               LinkedEntity = linkedEntity,
                                               LinkedEdge   = Entity.Null,
                                               Key          = key
                                           });
            // Position is at midpoint for hit detection
            EntityManager.AddComponentData(handle,
                                           new NT_HandlePosition { Position = (pointA + pointB) * 0.5f });
            EntityManager.AddComponentData(handle, NT_HandleLine.Create(pointA, pointB));

            m_Handles.Add(handle);
            return handle;
        }

        /// <summary>
        ///     Creates a circle handle for controlling a radius value.
        /// </summary>
        /// <param name="linkedEntity">The entity this handle controls.</param>
        /// <param name="key">Identifier key.</param>
        /// <param name="center">Center point of the circle.</param>
        /// <param name="radius">Size of the circle.</param>
        /// <param name="normal">Normal vector defining the circle's plane.</param>
        /// <param name="typeFlags">Type flags defining the handle's purpose.</param>
        /// <returns>The created handle entity.</returns>
        protected Entity CreateCircleHandle(
            Entity          linkedEntity,
            int             key,
            float3          center,
            float           radius,
            float3          normal,
            HandleTypeFlags typeFlags) {
            var handle = EntityManager.CreateEntity();

            EntityManager.AddComponentData(handle, NT_Handle.Create(typeFlags | HandleTypeFlags.Circle));
            EntityManager.AddComponentData(handle,
                                           new NT_HandleLink {
                                               LinkedEntity = linkedEntity,
                                               LinkedEdge   = Entity.Null,
                                               Key          = key
                                           });
            EntityManager.AddComponentData(handle, new NT_HandlePosition { Position = center });
            EntityManager.AddComponentData(handle, NT_HandleCircle.Create(radius, normal));

            m_Handles.Add(handle);
            return handle;
        }

        /// <summary>
        ///     Creates a rotation handle for controlling an angular value.
        ///     Composes <see cref="NT_HandleCircle"/> (shared geometry) with
        ///     <see cref="NT_HandleRotation"/> (rotation-specific data).
        /// </summary>
        /// <param name="linkedEntity">The entity this handle controls.</param>
        /// <param name="key">Identifier key.</param>
        /// <param name="center">Center point of the rotation circle.</param>
        /// <param name="radius">Size of the rotation circle.</param>
        /// <param name="normal">Normal vector defining the rotation plane.</param>
        /// <param name="referenceDirection">Zero-angle direction on the plane (must be perpendicular to normal).</param>
        /// <param name="angle">Initial angle in radians.</param>
        /// <param name="typeFlags">Type flags defining the handle's purpose.</param>
        /// <returns>The created handle entity.</returns>
        protected Entity CreateRotationHandle(
            Entity          linkedEntity,
            int             key,
            float3          center,
            float           radius,
            float3          normal,
            float3          referenceDirection,
            float           angle,
            HandleTypeFlags typeFlags) {
            var handle = EntityManager.CreateEntity();

            EntityManager.AddComponentData(handle, NT_Handle.Create(typeFlags | HandleTypeFlags.Rotation));
            EntityManager.AddComponentData(handle,
                                           new NT_HandleLink {
                                               LinkedEntity = linkedEntity,
                                               LinkedEdge   = Entity.Null,
                                               Key          = key
                                           });
            EntityManager.AddComponentData(handle, new NT_HandlePosition { Position = center });
            EntityManager.AddComponentData(handle, NT_HandleRotation.Create(radius, normal, referenceDirection, angle));

            m_Handles.Add(handle);
            return handle;
        }

        #endregion

        #region Handle Destruction

        /// <summary>
        ///     Destroys all handles created by this tool.
        /// </summary>
        protected void DestroyAllHandles() {
            if (!m_Handles.IsCreated) return;

            for (var i = 0; i < m_Handles.Length; i++) {
                var handle = m_Handles[i];
                if (EntityManager.Exists(handle)) {
                    EntityManager.DestroyEntity(handle);
                }
            }

            m_Handles.Clear();
            m_HandleEntries?.Clear();
            m_ParameterHandles?.Clear();
            m_ParentChildLinks?.Clear();
        }

        /// <summary>
        ///     Destroys all handles that have any of the specified flags.
        /// </summary>
        /// <param name="flags">Flags to match against.</param>
        protected void DestroyHandlesWithFlags(HandleTypeFlags flags) {
            if (!m_Handles.IsCreated) return;

            for (var i = m_Handles.Length - 1; i >= 0; i--) {
                var handle = m_Handles[i];
                if (!EntityManager.Exists(handle)) {
                    m_Handles.RemoveAtSwapBack(i);
                    continue;
                }

                var handleData = EntityManager.GetComponentData<NT_Handle>(handle);
                if (handleData.HasAnyFlag(flags)) {
                    EntityManager.DestroyEntity(handle);
                    m_Handles.RemoveAtSwapBack(i);
                }
            }
        }

        #endregion

        #region Handle Raycasting

        /// <summary>
        ///     Constructs a camera ray from the current mouse position.
        ///     Returns false if Camera.main is unavailable.
        /// </summary>
        /// <param name="rayOrigin">World-space origin of the ray.</param>
        /// <param name="rayDir">World-space direction of the ray.</param>
        protected bool TryGetCurrentMouseRay(out float3 rayOrigin, out float3 rayDir) {
            rayOrigin = float3.zero;
            rayDir    = float3.zero;

            var camera = Camera.main;
            if (camera == null) return false;

            var mousePos = Mouse.current.position.ReadValue();
            var ray      = camera.ScreenPointToRay(mousePos);
            rayOrigin = (float3)ray.origin;
            rayDir    = (float3)ray.direction;
            return true;
        }


        /// <summary>
        ///     Gets the closest handle entity from the current camera ray.
        ///     Performs type-aware intersection testing (point, line, circle).
        /// </summary>
        /// <returns>The closest handle entity, or Entity.Null if none hit.</returns>
        protected Entity GetClosestHandleFromRay() {
            if (!m_Handles.IsCreated || m_Handles.Length == 0) return Entity.Null;
            if (!TryGetCurrentMouseRay(out var rayOrigin, out var rayDir)) return Entity.Null;

            var closestHandle = Entity.Null;
            var closestT      = float.MaxValue;

            for (var i = 0; i < m_Handles.Length; i++) {
                var handleEntity = m_Handles[i];
                if (!EntityManager.Exists(handleEntity)) continue;

                var   handleData = EntityManager.GetComponentData<NT_Handle>(handleEntity);
                var   radius     = handleData.Size > 0f ? handleData.Size : NT_Handle.SizePrimary;
                float t;

                if (handleData.HasAnyFlag(HandleTypeFlags.Line)) {
                    var line = EntityManager.GetComponentData<NT_HandleLine>(handleEntity);
                    if (TryRayLineIntersection(rayOrigin, rayDir, line.PointA, line.PointB, radius, out t)) {
                        if (t < closestT) {
                            closestT      = t;
                            closestHandle = handleEntity;
                        }
                    }
                } else if (handleData.HasAnyFlag(HandleTypeFlags.Rotation)) {
                    var circlePos = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
                    var rotation  = EntityManager.GetComponentData<NT_HandleRotation>(handleEntity);
                    if (TryRayCircleIntersection(rayOrigin,
                                                 rayDir,
                                                 circlePos,
                                                 rotation.Radius,
                                                 rotation.Normal,
                                                 radius,
                                                 out t)) {
                        if (t < closestT) {
                            closestT      = t;
                            closestHandle = handleEntity;
                        }
                    }
                } else if (handleData.HasAnyFlag(HandleTypeFlags.Circle)) {
                    var circlePos = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
                    var circle    = EntityManager.GetComponentData<NT_HandleCircle>(handleEntity);
                    if (TryRayCircleIntersection(rayOrigin,
                                                 rayDir,
                                                 circlePos,
                                                 circle.Radius,
                                                 circle.Normal,
                                                 radius,
                                                 out t)) {
                        if (t < closestT) {
                            closestT      = t;
                            closestHandle = handleEntity;
                        }
                    }
                } else {
                    // Default: point/sphere intersection
                    var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
                    if (TryRaySphereIntersection(rayOrigin, rayDir, handlePos, radius, out t)) {
                        if (t < closestT) {
                            closestT      = t;
                            closestHandle = handleEntity;
                        }
                    }
                }
            }

            return closestHandle;
        }

        /// <summary>
        ///     Tests ray-sphere intersection for point handles.
        /// </summary>
        private bool TryRaySphereIntersection(float3    rayOrigin, float3 rayDir, float3 sphereCenter, float radius,
                                              out float t) {
            t = float.MaxValue;

            var oc           = rayOrigin - sphereCenter;
            var a            = math.dot(rayDir, rayDir);
            var b            = 2.0f * math.dot(oc, rayDir);
            var c            = math.dot(oc,        oc) - radius * radius;
            var discriminant = b                                * b - 4 * a * c;

            if (discriminant < 0) return false;

            t = (-b - math.sqrt(discriminant)) / (2.0f * a);
            return t >= 0;
        }

        /// <summary>
        ///     Tests ray-line intersection (closest point on line segment within threshold).
        /// </summary>
        private bool TryRayLineIntersection(float3 rayOrigin, float3    rayDir, float3 lineA, float3 lineB,
                                            float  threshold, out float t) {
            t = float.MaxValue;

            // Find closest point between ray and line segment
            var lineDir = lineB - lineA;
            var lineLen = math.length(lineDir);
            if (lineLen < 0.001f) {
                return TryRaySphereIntersection(rayOrigin, rayDir, lineA, threshold, out t);
            }

            lineDir /= lineLen;

            var w0    = rayOrigin - lineA;
            var a     = math.dot(rayDir,  rayDir);
            var b     = math.dot(rayDir,  lineDir);
            var c     = math.dot(lineDir, lineDir);
            var d     = math.dot(rayDir,  w0);
            var e     = math.dot(lineDir, w0);
            var denom = a * c - b * b;

            if (math.abs(denom) < 0.0001f) return false;

            var tRay  = (b * e - c * d) / denom;
            var tLine = (a * e - b * d) / denom;

            // Clamp to line segment
            tLine = math.clamp(tLine, 0, lineLen);

            var closestOnRay  = rayOrigin + rayDir  * tRay;
            var closestOnLine = lineA     + lineDir * tLine;
            var dist          = math.distance(closestOnRay, closestOnLine);

            if (dist <= threshold && tRay >= 0) {
                t = tRay;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Tests ray-circle intersection (intersection with circle arc within threshold).
        /// </summary>
        private bool TryRayCircleIntersection(float3 rayOrigin, float3 rayDir,    float3    center, float radius,
                                              float3 normal,    float  threshold, out float t) {
            t = float.MaxValue;

            // Intersect ray with the plane defined by center and normal
            var denom = math.dot(normal, rayDir);
            if (math.abs(denom) < 0.0001f) return false;

            var planeT = math.dot(center - rayOrigin, normal) / denom;
            if (planeT < 0) return false;

            var planeHit       = rayOrigin + rayDir * planeT;
            var distFromCenter = math.distance(planeHit, center);

            // Check if hit is on or near the circle
            if (math.abs(distFromCenter - radius) <= threshold) {
                t = planeT;
                return true;
            }

            return false;
        }

        #endregion

        #region Handle Dragging

        /// <summary>
        ///     Updates the dragged handle's position by projecting mouse onto appropriate plane.
        ///     Applies any constraints defined on the handle.
        ///     Circle handles are excluded — their radius is computed directly from the mouse ray.
        /// </summary>
        /// <param name="handleEntity">The handle entity to update.</param>
        private void UpdateHandleDragPosition(Entity handleEntity) {
            if (!EntityManager.Exists(handleEntity)) return;
            if (!EntityManager.HasComponent<NT_HandlePosition>(handleEntity)) return;

            // Circle and rotation handles don't move their position; they are computed separately
            if (EntityManager.HasComponent<NT_HandleCircle>(handleEntity)) return;
            if (EntityManager.HasComponent<NT_HandleRotation>(handleEntity)) return;

            var currentPos = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
            var newPos     = currentPos;

            // Check for constraints
            if (EntityManager.HasComponent<NT_HandleConstraints>(handleEntity)) {
                var constraints = EntityManager.GetComponentData<NT_HandleConstraints>(handleEntity);
                newPos = ApplyConstrainedMovement(currentPos, constraints);
            } else {
                // Default: project onto XZ plane at handle's Y
                if (TryGetXZPlaneIntersection(currentPos.y, out var intersection)) {
                    newPos = intersection;
                }
            }

            EntityManager.SetComponentData(handleEntity,
                                           new NT_HandlePosition { Position = newPos });

            // Update line handle endpoints if applicable
            if (EntityManager.HasComponent<NT_HandleLine>(handleEntity)) {
                var line  = EntityManager.GetComponentData<NT_HandleLine>(handleEntity);
                var delta = newPos - currentPos;
                line.PointA += delta;
                line.PointB += delta;
                EntityManager.SetComponentData(handleEntity, line);
            }
        }

        /// <summary>
        ///     Computes a circle handle's new radius from the current mouse position
        ///     projected onto the XZ plane at the circle center's Y.
        ///     Does not move the handle's position.
        /// </summary>
        /// <param name="handleEntity">The circle handle entity being dragged.</param>
        /// <returns>The newly computed radius, or the current radius if projection fails.</returns>
        private float ComputeCircleHandleRadius(Entity handleEntity) {
            var center = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
            var circle = EntityManager.GetComponentData<NT_HandleCircle>(handleEntity);

            if (!TryGetXZPlaneIntersection(center.y, out var dragPos)) {
                return circle.Radius;
            }

            var newRadius = math.distance(center.xz, dragPos.xz);

            circle.Radius = newRadius;
            EntityManager.SetComponentData(handleEntity, circle);

            return newRadius;
        }

        /// <summary>
        ///     Computes the raw angle of the current mouse position on the rotation plane,
        ///     without applying any offset. Used to capture the initial grab point.
        /// </summary>
        private float ComputeRawRotationAngle(Entity handleEntity) {
            var center   = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
            var rotation = EntityManager.GetComponentData<NT_HandleRotation>(handleEntity);

            if (!TryGetCurrentMouseRay(out var rayOrigin, out var rayDir)) {
                return rotation.Angle;
            }

            var denom = math.dot(rotation.Normal, rayDir);
            if (math.abs(denom) < 0.0001f) {
                return rotation.Angle;
            }

            var planeT = math.dot(center - rayOrigin, rotation.Normal) / denom;
            if (planeT < 0) {
                return rotation.Angle;
            }

            var planeHit = rayOrigin + rayDir * planeT;
            var toHit    = planeHit - center;

            var perpendicular = math.cross(rotation.Normal, rotation.ReferenceDirection);
            var x             = math.dot(toHit, rotation.ReferenceDirection);
            var y             = math.dot(toHit, perpendicular);

            return math.atan2(y, x);
        }

        /// <summary>
        ///     Computes a rotation handle's new angle from the current mouse position,
        ///     applying the drag offset so rotation is relative to the grab point.
        /// </summary>
        private float ComputeRotationHandleAngle(Entity handleEntity) {
            var rotation = EntityManager.GetComponentData<NT_HandleRotation>(handleEntity);
            var rawAngle = ComputeRawRotationAngle(handleEntity);

            if (math.abs(rawAngle - rotation.Angle) < 0.00001f) {
                return rotation.Angle;
            }

            var newAngle = rawAngle - m_RotationDragOffset;

            rotation.Angle = newAngle;
            EntityManager.SetComponentData(handleEntity, rotation);

            return newAngle;
        }

        /// <summary>
        ///     Applies movement constraints to compute a new position.
        /// </summary>
        private float3 ApplyConstrainedMovement(float3 currentPos, NT_HandleConstraints constraints) {
            var newPos = currentPos;

            if (constraints.HasFlag(ConstraintFlags.SnapToAxis)) {
                // Project mouse onto the axis line
                if (TryGetAxisIntersection(constraints.Origin, constraints.SnapAxis, out var axisPoint)) {
                    newPos = axisPoint;

                    // Apply axis distance clamping if enabled
                    if (constraints.HasFlag(ConstraintFlags.ClampAxisDistance)) {
                        var distanceFromOrigin = math.dot(newPos - constraints.Origin, constraints.SnapAxis);
                        var clampedDistance = math.clamp(distanceFromOrigin,
                                                         constraints.MinAxisDistance,
                                                         constraints.MaxAxisDistance);
                        newPos = constraints.Origin + constraints.SnapAxis * clampedDistance;
                    }
                }
            } else {
                // Determine which plane to project onto based on lock flags
                if (constraints.HasFlag(ConstraintFlags.LockY)) {
                    if (TryGetXZPlaneIntersection(currentPos.y, out var intersection)) {
                        newPos = intersection;
                    }
                } else if (constraints.HasFlag(ConstraintFlags.LockX)) {
                    if (TryGetYZPlaneIntersection(currentPos.x, out var intersection)) {
                        newPos = intersection;
                    }
                } else if (constraints.HasFlag(ConstraintFlags.LockZ)) {
                    if (TryGetXYPlaneIntersection(currentPos.z, out var intersection)) {
                        newPos = intersection;
                    }
                } else {
                    // No axis lock, use XZ plane by default
                    if (TryGetXZPlaneIntersection(currentPos.y, out var intersection)) {
                        newPos = intersection;
                    }
                }
            }

            // Apply bounds clamping
            if (constraints.HasFlag(ConstraintFlags.ClampToBounds)) {
                newPos = math.clamp(newPos, constraints.MinBounds, constraints.MaxBounds);
            }

            return newPos;
        }

        /// <summary>
        ///     Gets the intersection point of the camera ray with a horizontal plane at the specified Y.
        /// </summary>
        protected bool TryGetXZPlaneIntersection(float planeY, out float3 intersection) {
            intersection = float3.zero;
            if (!TryGetCurrentMouseRay(out var rayOrigin, out var rayDir)) return false;

            if (math.abs(rayDir.y) < 0.0001f) return false;

            var t = (planeY - rayOrigin.y) / rayDir.y;
            if (t < 0) return false;

            intersection = rayOrigin + rayDir * t;
            return true;
        }

        /// <summary>
        ///     Gets the intersection point of the camera ray with a YZ plane at the specified X.
        /// </summary>
        protected bool TryGetYZPlaneIntersection(float planeX, out float3 intersection) {
            intersection = float3.zero;
            if (!TryGetCurrentMouseRay(out var rayOrigin, out var rayDir)) return false;

            if (math.abs(rayDir.x) < 0.0001f) return false;

            var t = (planeX - rayOrigin.x) / rayDir.x;
            if (t < 0) return false;

            intersection = rayOrigin + rayDir * t;
            return true;
        }

        /// <summary>
        ///     Gets the intersection point of the camera ray with a XY plane at the specified Z.
        /// </summary>
        protected bool TryGetXYPlaneIntersection(float planeZ, out float3 intersection) {
            intersection = float3.zero;
            if (!TryGetCurrentMouseRay(out var rayOrigin, out var rayDir)) return false;

            if (math.abs(rayDir.z) < 0.0001f) return false;

            var t = (planeZ - rayOrigin.z) / rayDir.z;
            if (t < 0) return false;

            intersection = rayOrigin + rayDir * t;
            return true;
        }

        /// <summary>
        ///     Gets the closest point on an axis line from the camera ray.
        /// </summary>
        protected bool TryGetAxisIntersection(float3 axisOrigin, float3 axisDir, out float3 intersection) {
            intersection = axisOrigin;
            if (!TryGetCurrentMouseRay(out var rayOrigin, out var rayDir)) return false;

            // Find closest point between two lines
            var w0    = rayOrigin - axisOrigin;
            var a     = math.dot(rayDir,  rayDir);
            var b     = math.dot(rayDir,  axisDir);
            var c     = math.dot(axisDir, axisDir);
            var d     = math.dot(rayDir,  w0);
            var e     = math.dot(axisDir, w0);
            var denom = a * c - b * b;

            if (math.abs(denom) < 0.0001f) return false;

            var tAxis = (a * e - b * d) / denom;
            intersection = axisOrigin + axisDir * tAxis;
            return true;
        }

        #endregion

        #region Virtual Hooks

        /// <summary>
        ///     Called when a handle drag operation starts.
        /// </summary>
        /// <param name="handle">The handle entity being dragged.</param>
        protected virtual void OnHandleDragStart(Entity handle) {
            // Override in derived tools
        }

        /// <summary>
        ///     Called when a handle drag operation ends.
        /// </summary>
        /// <param name="handle">The handle entity that was dragged.</param>
        protected virtual void OnHandleDragEnd(Entity handle) {
            // Override in derived tools
        }

        /// <summary>
        ///     Called when a handle is clicked (mouse down + up without dragging).
        /// </summary>
        /// <param name="handle">The handle entity that was clicked.</param>
        protected virtual void OnHandleClick(Entity handle) {
            // Override in derived tools
        }

        #endregion

        #region Handle Input Processing

        /// <summary>
        ///     Processes handle input for the current frame.
        /// </summary>
        /// <returns>True if input was consumed by handle interaction.</returns>
        protected bool ProcessHandleInput() {
            if (!ShouldRaycastHandles) return false;

            switch (m_HandleInputState) {
                case HandleInputState.Idle:
                    return ProcessHandleIdleState();

                case HandleInputState.PendingAction:
                    return ProcessHandlePendingState();

                case HandleInputState.Dragging:
                    return ProcessHandleDraggingState();
            }

            return false;
        }

        private bool ProcessHandleIdleState() {
            var hoveredHandle = GetClosestHandleFromRay();

            // Update hover highlighting
            if (hoveredHandle != m_LastHoveredEntity.Value) {
                if (m_LastHoveredEntity.Value != Entity.Null        &&
                    EntityManager.Exists(m_LastHoveredEntity.Value) &&
                    EntityManager.HasComponent<NT_Highlighted>(m_LastHoveredEntity.Value)) {
                    EntityManager.RemoveComponent<NT_Highlighted>(m_LastHoveredEntity.Value);
                }

                if (hoveredHandle != Entity.Null) {
                    EntityManager.AddComponentData(hoveredHandle, NT_Highlighted.DefaultNode);
                }

                m_LastHoveredEntity.Value = hoveredHandle;
            }

            // Check for mouse down on a handle
            if (hoveredHandle != Entity.Null && m_ApplyAction.WasPressedThisFrame()) {
                m_HandleInputState = HandleInputState.PendingAction;
                m_DraggedHandle    = hoveredHandle;

                if (TryGetXZPlaneIntersection(EntityManager.GetComponentData<NT_HandlePosition>(hoveredHandle).Position
                                                           .y,
                                              out var hitPos)) {
                    m_HandleMouseDownPosition = hitPos;
                }

                return true;
            }

            return hoveredHandle != Entity.Null;
        }

        private bool ProcessHandlePendingState() {
            if (m_ApplyAction.WasReleasedThisFrame()) {
                // Released before drag threshold - this is a CLICK
                OnHandleClick(m_DraggedHandle);
                m_HandleInputState = HandleInputState.Idle;
                m_DraggedHandle    = Entity.Null;
                return true;
            }

            if (!m_ApplyAction.IsPressed()) {
                m_HandleInputState = HandleInputState.Idle;
                m_DraggedHandle    = Entity.Null;
                return false;
            }

            // Check if we've moved enough to be considered a drag
            var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(m_DraggedHandle).Position;
            if (TryGetXZPlaneIntersection(handlePos.y, out var currentPos)) {
                var distance = math.distance(currentPos.xz, m_HandleMouseDownPosition.xz);
                if (distance > HandleDragThreshold) {
                    m_HandleInputState = HandleInputState.Dragging;

                    // Mark handle as selected, remove hover highlight
                    if (EntityManager.HasComponent<NT_Highlighted>(m_DraggedHandle)) {
                        EntityManager.RemoveComponent<NT_Highlighted>(m_DraggedHandle);
                    }

                    EntityManager.AddComponentData(m_DraggedHandle, NT_Selected.DefaultNode);

                    // For rotation handles, capture the offset between where the user
                    // clicked and the handle's current angle so dragging is relative.
                    if (EntityManager.HasComponent<NT_HandleRotation>(m_DraggedHandle)) {
                        var currentAngle = EntityManager.GetComponentData<NT_HandleRotation>(m_DraggedHandle).Angle;
                        var mouseAngle   = ComputeRawRotationAngle(m_DraggedHandle);
                        m_RotationDragOffset = mouseAngle - currentAngle;
                    }

                    OnHandleDragStart(m_DraggedHandle);
                    return true;
                }
            }

            return true;
        }

        private bool ProcessHandleDraggingState() {
            if (m_ApplyAction.WasReleasedThisFrame()) {
                // Drag ended
                if (EntityManager.HasComponent<NT_Selected>(m_DraggedHandle)) {
                    EntityManager.RemoveComponent<NT_Selected>(m_DraggedHandle);
                }

                OnHandleDragEnd(m_DraggedHandle);

                m_HandleInputState = HandleInputState.Idle;
                m_DraggedHandle    = Entity.Null;
                return true;
            }

            // Update position (skipped for circle and rotation handles)
            UpdateHandleDragPosition(m_DraggedHandle);

            var handleData = EntityManager.GetComponentData<NT_Handle>(m_DraggedHandle);

            if (handleData.HasAnyFlag(HandleTypeFlags.Rotation)
                    && EntityManager.HasComponent<NT_HandleRotation>(m_DraggedHandle)) {
                var newAngle  = ComputeRotationHandleAngle(m_DraggedHandle);
                var rotation  = EntityManager.GetComponentData<NT_HandleRotation>(m_DraggedHandle);
                DispatchRotationDrag(m_DraggedHandle, newAngle, rotation.GetDirection());
            } else if (handleData.HasAnyFlag(HandleTypeFlags.Circle)) {
                var newRadius = ComputeCircleHandleRadius(m_DraggedHandle);
                DispatchCircleDrag(m_DraggedHandle, newRadius);
            } else {
                var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(m_DraggedHandle).Position;
                DispatchDrag(m_DraggedHandle, handlePos);
            }

            m_UpdateNeeded = true;
            return true;
        }

        /// <summary>
        ///     Cancels any in-progress handle interaction.
        /// </summary>
        protected void CancelHandleInteraction() {
            if (m_DraggedHandle != Entity.Null) {
                if (EntityManager.Exists(m_DraggedHandle)) {
                    if (EntityManager.HasComponent<NT_Selected>(m_DraggedHandle)) {
                        EntityManager.RemoveComponent<NT_Selected>(m_DraggedHandle);
                    }
                }

                m_DraggedHandle = Entity.Null;
            }

            m_HandleInputState = HandleInputState.Idle;
        }

        #endregion
    }
}
