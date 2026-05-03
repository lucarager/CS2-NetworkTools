namespace NetworkTools.Systems.Tools {
    using System.Collections.Generic;

    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Base;
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
        ///     Entity query for all handle entities.
        /// </summary>
        protected EntityQuery m_HandleQuery;

        /// <summary>
        ///     Maps handle entities to the parameters they control.
        ///     Populated during <see cref="CreateHandlesFromDefinitions"/> from
        ///     <see cref="TransformHandleDefinition.Parameter"/> references.
        /// </summary>
        protected Dictionary<Entity, ParameterBase> m_HandleParameterMap;

        /// <summary>
        ///     Initializes handle management. Called from OnCreate().
        /// </summary>
        protected void InitializeHandles() {
            m_Handles             = new NativeList<Entity>(16, Allocator.Persistent);
            m_HandleParameterMap  = new Dictionary<Entity, ParameterBase>(16);
            m_HandleInputState    = HandleInputState.Idle;
            m_DraggedHandle       = Entity.Null;

            m_HandleQuery = SystemAPI.QueryBuilder()
                                     .WithAll<NT_Handle, NT_HandlePosition, NT_HandleLink>()
                                     .Build();
        }

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
            float                 radius = NT_Handle.PrimaryRadius) {
            var handle = EntityManager.CreateEntity();

            EntityManager.AddComponentData(handle, NT_Handle.Create(typeFlags | HandleTypeFlags.Position, radius));
            EntityManager.AddComponentData(handle,
                                           new NT_HandleLink {
                                               LinkedEntity = linkedEntity,
                                               LinkedEdge   = linkedEdge,
                                               Key          = key
                                           });
            EntityManager.AddComponentData(handle,
                                           new NT_HandlePosition {
                                               Position = position,
                                               Rotation = quaternion.identity
                                           });

            if (constraints.HasValue) {
                EntityManager.AddComponentData(handle, constraints.Value);
            }

            m_Handles.Add(handle);
            return handle;
        }

        /// <summary>
        ///     Creates a parameter handle for controlling a scalar value.
        /// </summary>
        /// <param name="linkedEntity">The entity whose parameter this handle controls.</param>
        /// <param name="key">Identifier key for the parameter.</param>
        /// <param name="position">World position of the handle.</param>
        /// <param name="value">Current parameter value.</param>
        /// <param name="minValue">Minimum allowed value.</param>
        /// <param name="maxValue">Maximum allowed value.</param>
        /// <param name="typeFlags">Type flags defining the handle's purpose.</param>
        /// <param name="constraints">Optional movement constraints.</param>
        /// <param name="radius">Hit detection and visual radius.</param>
        /// <returns>The created handle entity.</returns>
        protected Entity CreateParameterHandle(
            Entity                linkedEntity,
            int                   key,
            float3                position,
            float                 value,
            float                 minValue,
            float                 maxValue,
            HandleTypeFlags       typeFlags,
            NT_HandleConstraints? constraints = null,
            float                 radius = NT_Handle.PrimaryRadius) {
            var handle = EntityManager.CreateEntity();

            EntityManager.AddComponentData(handle, NT_Handle.Create(typeFlags | HandleTypeFlags.Parameter, radius));
            EntityManager.AddComponentData(handle,
                                           new NT_HandleLink {
                                               LinkedEntity = linkedEntity,
                                               LinkedEdge   = Entity.Null,
                                               Key          = key
                                           });
            EntityManager.AddComponentData(handle,
                                           new NT_HandlePosition {
                                               Position = position,
                                               Rotation = quaternion.identity
                                           });
            EntityManager.AddComponentData(handle, NT_HandleValue.Create(value, minValue, maxValue));

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
                                           new NT_HandlePosition {
                                               Position = (pointA + pointB) * 0.5f,
                                               Rotation = quaternion.identity
                                           });
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
        /// <param name="radius">Radius of the circle.</param>
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
            // Position is at center for reference
            EntityManager.AddComponentData(handle,
                                           new NT_HandlePosition {
                                               Position = center,
                                               Rotation = quaternion.identity
                                           });
            EntityManager.AddComponentData(handle, NT_HandleCircle.Create(center, radius, normal));

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
        /// <param name="radius">Radius of the rotation circle.</param>
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
            // Position is at center for reference
            EntityManager.AddComponentData(handle,
                                           new NT_HandlePosition {
                                               Position = center,
                                               Rotation = quaternion.identity
                                           });
            // Shared circle geometry for hit detection and rendering
            EntityManager.AddComponentData(handle, NT_HandleCircle.Create(center, radius, normal));
            // Rotation-specific data
            EntityManager.AddComponentData(handle, NT_HandleRotation.Create(referenceDirection, angle));

            m_Handles.Add(handle);
            return handle;
        }

        /// <summary>
        ///     Creates handle entities from an array of definitions.
        ///     Two-pass: roots first (ParentKey == NoParent), then children with NT_HandleParent resolved.
        ///     Populates the handle-to-parameter map from <see cref="TransformHandleDefinition.Parameter"/>.
        /// </summary>
        /// <param name="definitions">Handle definitions describing position, type, and parent relationships.</param>
        protected void CreateHandlesFromDefinitions(TransformHandleDefinition[] definitions) {
            if (definitions == null || definitions.Length == 0) return;

            // Key → Entity mapping for resolving parent references
            var keyToEntity = new Dictionary<int, Entity>(definitions.Length);

            // Pass 1: create root handles (no parent)
            foreach (var def in definitions) {
                if (def.ParentKey != TransformHandleDefinition.NoParent) {
                    continue;
                }

                var entity = CreateHandleFromDefinition(def);
                keyToEntity[def.Key] = entity;

                if (def.Parameter != null) {
                    m_HandleParameterMap[entity] = def.Parameter;
                }
            }

            // Pass 2: create child handles and attach NT_HandleParent
            foreach (var def in definitions) {
                if (def.ParentKey == TransformHandleDefinition.NoParent) {
                    continue;
                }

                var entity = CreateHandleFromDefinition(def);
                keyToEntity[def.Key] = entity;

                if (def.Parameter != null) {
                    m_HandleParameterMap[entity] = def.Parameter;
                }

                if (keyToEntity.TryGetValue(def.ParentKey, out var parentEntity)) {
                    EntityManager.AddComponentData(entity, new NT_HandleParent { Parent = parentEntity });
                }
            }
        }

        /// <summary>
        ///     Creates a single handle entity from a definition, dispatching by type flags.
        /// </summary>
        private Entity CreateHandleFromDefinition(TransformHandleDefinition def) {
            var radius = def.Radius > 0f ? def.Radius : NT_Handle.PrimaryRadius;

            if ((def.TypeFlags & HandleTypeFlags.Parameter) != 0) {
                return CreateParameterHandle(
                    Entity.Null,
                    def.Key,
                    def.Position,
                    def.Value,
                    def.MinValue,
                    def.MaxValue,
                    def.TypeFlags,
                    def.Constraints,
                    radius);
            }

            if ((def.TypeFlags & HandleTypeFlags.Circle) != 0)
            {
                return CreateCircleHandle(
                    Entity.Null,
                    def.Key,
                    def.Position,
                    def.Value,
                    new float3(0f, 1f, 0f),
                    def.TypeFlags);
            }

            if ((def.TypeFlags & HandleTypeFlags.Rotation) != 0)
            {
                var normal = math.lengthsq(def.Normal) > 0f
                    ? math.normalizesafe(def.Normal)
                    : new float3(0f, 1f, 0f);
                var refDir = math.lengthsq(def.ReferenceDirection) > 0f
                    ? math.normalizesafe(def.ReferenceDirection)
                    : new float3(1f, 0f, 0f);
                return CreateRotationHandle(
                    Entity.Null,
                    def.Key,
                    def.Position,
                    def.Value > 0f ? def.Value : 10f,
                    normal,
                    refDir,
                    def.Angle,
                    def.TypeFlags);
            }

            return CreatePositionHandle(
                Entity.Null,
                Entity.Null,
                def.Key,
                def.Position,
                def.TypeFlags,
                def.Constraints,
                radius);
        }

        /// <summary>
        ///     Moves all child handles of the given parent by the specified delta.
        ///     Updates the position of each child that has an NT_HandleParent pointing to the parent.
        /// </summary>
        /// <param name="parentHandle">The parent handle entity whose children should move.</param>
        /// <param name="delta">The world-space offset to apply to each child.</param>
        protected void PropagateToChildren(Entity parentHandle, float3 delta) {
            for (var i = 0; i < m_Handles.Length; i++) {
                var child = m_Handles[i];
                if (!EntityManager.HasComponent<NT_HandleParent>(child)) {
                    continue;
                }

                var parentComponent = EntityManager.GetComponentData<NT_HandleParent>(child);
                if (parentComponent.Parent != parentHandle) {
                    continue;
                }

                var childPos = EntityManager.GetComponentData<NT_HandlePosition>(child);
                childPos.Position += delta;
                EntityManager.SetComponentData(child, childPos);

                // Keep circle handle center in sync with position for hit detection
                if (EntityManager.HasComponent<NT_HandleCircle>(child)) {
                    var circle = EntityManager.GetComponentData<NT_HandleCircle>(child);
                    circle.Center += delta;
                    EntityManager.SetComponentData(child, circle);
                }
            }
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
            m_HandleParameterMap?.Clear();
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
                var   radius     = handleData.Radius > 0f ? handleData.Radius : NT_Handle.PrimaryRadius;
                float t;

                if (handleData.HasAnyFlag(HandleTypeFlags.Line)) {
                    var line = EntityManager.GetComponentData<NT_HandleLine>(handleEntity);
                    if (TryRayLineIntersection(rayOrigin, rayDir, line.PointA, line.PointB, radius, out t)) {
                        if (t < closestT) {
                            closestT      = t;
                            closestHandle = handleEntity;
                        }
                    }
                } else if (handleData.HasAnyFlag(HandleTypeFlags.Circle | HandleTypeFlags.Rotation)) {
                    // Both circle and rotation handles share NT_HandleCircle for geometry
                    var circle = EntityManager.GetComponentData<NT_HandleCircle>(handleEntity);
                    if (TryRayCircleIntersection(rayOrigin,
                                                 rayDir,
                                                 circle.Center,
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

            // Circle-based handles (including rotation) don't move their position; they are computed separately
            if (EntityManager.HasComponent<NT_HandleCircle>(handleEntity)) return;

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
                                           new NT_HandlePosition {
                                               Position = newPos,
                                               Rotation = quaternion.identity
                                           });

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
            var circle = EntityManager.GetComponentData<NT_HandleCircle>(handleEntity);

            if (!TryGetXZPlaneIntersection(circle.Center.y, out var dragPos)) {
                return circle.Radius;
            }

            var newRadius = math.distance(circle.Center.xz, dragPos.xz);

            circle.Radius = newRadius;
            EntityManager.SetComponentData(handleEntity, circle);

            return newRadius;
        }

        /// <summary>
        ///     Computes a rotation handle's new angle from the current mouse position
        ///     projected onto the circle's plane.
        ///     Reads geometry from <see cref="NT_HandleCircle"/> and updates <see cref="NT_HandleRotation"/>.
        /// </summary>
        /// <param name="handleEntity">The rotation handle entity being dragged.</param>
        /// <returns>The newly computed angle in radians, or the current angle if projection fails.</returns>
        private float ComputeRotationHandleAngle(Entity handleEntity) {
            var rotation = EntityManager.GetComponentData<NT_HandleRotation>(handleEntity);
            var circle   = EntityManager.GetComponentData<NT_HandleCircle>(handleEntity);

            // Intersect mouse ray with the rotation plane
            if (!TryGetCurrentMouseRay(out var rayOrigin, out var rayDir)) {
                return rotation.Angle;
            }

            var denom = math.dot(circle.Normal, rayDir);
            if (math.abs(denom) < 0.0001f) {
                return rotation.Angle;
            }

            var planeT = math.dot(circle.Center - rayOrigin, circle.Normal) / denom;
            if (planeT < 0) {
                return rotation.Angle;
            }

            var planeHit = rayOrigin + rayDir * planeT;
            var toHit    = planeHit - circle.Center;

            // Project onto the plane's local 2D axes
            var perpendicular = math.cross(circle.Normal, rotation.ReferenceDirection);
            var x             = math.dot(toHit, rotation.ReferenceDirection);
            var y             = math.dot(toHit, perpendicular);
            var newAngle      = math.atan2(y, x);

            rotation.Angle = newAngle;
            EntityManager.SetComponentData(handleEntity, rotation);

            return newAngle;
        }

        /// <summary>
        ///     Handles dragging for position-based handles.
        ///     Reads the previous position from the mapped parameter, writes the new position,
        ///     propagates movement to child handles, and syncs their parameters.
        /// </summary>
        /// <param name="handle">The position handle entity being dragged.</param>
        /// <param name="handlePos">The handle's current world position.</param>
        protected void HandlePositionDrag(Entity handle, float3 handlePos) {
            if (!m_HandleParameterMap.TryGetValue(handle, out var param) || !(param is Float3Parameter f3p)) {
                return;
            }

            var previousPos = f3p.Value;
            var delta = handlePos - previousPos;

            f3p.Value = handlePos;

            if (math.lengthsq(delta) > 0f) {
                PropagateToChildren(handle, delta);
                SyncChildParameterPositions(handle);
            }
        }

        /// <summary>
        ///     Syncs parameter positions for all children of the given parent handle
        ///     by reading each child's current position and writing it to the mapped parameter.
        /// </summary>
        /// <param name="parentHandle">The parent handle whose children should be synced.</param>
        protected void SyncChildParameterPositions(Entity parentHandle) {
            for (var i = 0; i < m_Handles.Length; i++) {
                var child = m_Handles[i];
                if (!EntityManager.HasComponent<NT_HandleParent>(child)) {
                    continue;
                }

                var parentComponent = EntityManager.GetComponentData<NT_HandleParent>(child);
                if (parentComponent.Parent != parentHandle) {
                    continue;
                }

                var childPos = EntityManager.GetComponentData<NT_HandlePosition>(child).Position;
                if (m_HandleParameterMap.TryGetValue(child, out var childParam) && childParam is Float3Parameter f3p) {
                    f3p.Value = childPos;
                }
            }
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

        /// <summary>
        ///     Called each frame while dragging a position handle.
        ///     Default implementation auto-dispatches to the mapped <see cref="Float3Parameter"/>
        ///     via <see cref="HandlePositionDrag"/>.
        /// </summary>
        /// <param name="handle">The handle entity being dragged.</param>
        /// <param name="position">The handle's updated world position.</param>
        protected virtual void OnPositionHandleDragged(Entity handle, float3 position) {
            HandlePositionDrag(handle, position);
        }

        /// <summary>
        ///     Called each frame while dragging a parameter handle.
        ///     Override for custom computation before setting the parameter value.
        /// </summary>
        /// <param name="handle">The handle entity being dragged.</param>
        /// <param name="position">The handle's updated world position.</param>
        /// <param name="value">The current <see cref="NT_HandleValue.Value"/>.</param>
        protected virtual void OnParameterHandleDragged(Entity handle, float3 position, float value) {
            // Override in derived tools for custom computation
        }

        /// <summary>
        ///     Called each frame while dragging a circle handle.
        ///     Default implementation auto-dispatches to the mapped <see cref="FloatParameter"/>.
        /// </summary>
        /// <param name="handle">The circle handle entity being dragged.</param>
        /// <param name="radius">The newly computed radius.</param>
        protected virtual void OnCircleHandleDragged(Entity handle, float radius) {
            if (m_HandleParameterMap.TryGetValue(handle, out var param) && param is FloatParameter fp) {
                fp.Value = radius;
            }
        }

        /// <summary>
        ///     Called each frame while dragging a rotation handle.
        ///     Default implementation auto-dispatches the direction to the mapped <see cref="Float3Parameter"/>.
        /// </summary>
        /// <param name="handle">The rotation handle entity being dragged.</param>
        /// <param name="angle">The newly computed angle in radians.</param>
        /// <param name="direction">The unit direction on the rotation plane at the current angle.</param>
        protected virtual void OnRotationHandleDragged(Entity handle, float angle, float3 direction) {
            if (m_HandleParameterMap.TryGetValue(handle, out var param) && param is Float3Parameter f3p) {
                f3p.Value = direction;
            }
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
                var circle    = EntityManager.GetComponentData<NT_HandleCircle>(m_DraggedHandle);
                OnRotationHandleDragged(m_DraggedHandle, newAngle, rotation.GetDirection(circle.Normal));
            } else if (handleData.HasAnyFlag(HandleTypeFlags.Circle)) {
                var newRadius = ComputeCircleHandleRadius(m_DraggedHandle);
                OnCircleHandleDragged(m_DraggedHandle, newRadius);
            } else if (handleData.HasAnyFlag(HandleTypeFlags.Parameter)
                    && EntityManager.HasComponent<NT_HandleValue>(m_DraggedHandle)) {
                var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(m_DraggedHandle).Position;
                var value     = EntityManager.GetComponentData<NT_HandleValue>(m_DraggedHandle).Value;
                OnParameterHandleDragged(m_DraggedHandle, handlePos, value);
            } else {
                var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(m_DraggedHandle).Position;
                OnPositionHandleDragged(m_DraggedHandle, handlePos);
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
