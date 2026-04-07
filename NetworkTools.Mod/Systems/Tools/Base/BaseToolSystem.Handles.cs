namespace NetworkTools.Systems.Tools {
    using System.Collections.Generic;

    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Base;

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
        #region Virtual Properties

        /// <summary>
        ///     Override to return true when the tool should perform handle raycasting.
        ///     Default returns true when handles exist.
        /// </summary>
        protected virtual bool ShouldRaycastHandles => m_Handles.IsCreated && m_Handles.Length > 0;

        #endregion

        #region Constants

        /// <summary>
        ///     World units the mouse must move before being considered a drag.
        /// </summary>
        protected const float HandleDragThreshold = 0.5f;

        #endregion

        #region Handle State

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

        #endregion

        #region Lifecycle (Called by NT_BaseToolSystem)

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

        #endregion

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
        ///     Creates handle entities from an array of definitions.
        ///     Two-pass: roots first (ParentKey == NoParent), then children with NT_HandleParent resolved.
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
            }

            // Pass 2: create child handles and attach NT_HandleParent
            foreach (var def in definitions) {
                if (def.ParentKey == TransformHandleDefinition.NoParent) {
                    continue;
                }

                var entity = CreateHandleFromDefinition(def);
                keyToEntity[def.Key] = entity;

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
                    default,
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
        ///     Gets the closest handle entity from the current camera ray.
        ///     Performs type-aware intersection testing (point, line, circle).
        /// </summary>
        /// <returns>The closest handle entity, or Entity.Null if none hit.</returns>
        protected Entity GetClosestHandleFromRay() {
            if (!m_Handles.IsCreated || m_Handles.Length == 0) return Entity.Null;

            var camera = Camera.main;
            if (camera == null) return Entity.Null;

            var mousePos  = Mouse.current.position.ReadValue();
            var ray       = camera.ScreenPointToRay(mousePos);
            var rayOrigin = (float3)ray.origin;
            var rayDir    = (float3)ray.direction;

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
                } else if (handleData.HasAnyFlag(HandleTypeFlags.Circle)) {
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
        /// </summary>
        /// <param name="handleEntity">The handle entity to update.</param>
        protected void UpdateHandleDragPosition(Entity handleEntity) {
            if (!EntityManager.Exists(handleEntity)) return;
            if (!EntityManager.HasComponent<NT_HandlePosition>(handleEntity)) return;

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

            // Update circle handle center if applicable
            if (EntityManager.HasComponent<NT_HandleCircle>(handleEntity)) {
                var circle = EntityManager.GetComponentData<NT_HandleCircle>(handleEntity);
                circle.Center = newPos;
                EntityManager.SetComponentData(handleEntity, circle);
            }
        }

        /// <summary>
        ///     Updates a circle handle's radius based on the XZ distance from the drag position
        ///     to the specified center, and resets the handle's position back to the center.
        /// </summary>
        /// <param name="handleEntity">The circle handle entity being dragged.</param>
        /// <param name="center">The center point to measure radius from.</param>
        /// <returns>The newly computed radius.</returns>
        protected float UpdateCircleHandleRadius(Entity handleEntity, float3 center) {
            var dragPos = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
            var newRadius = math.distance(center.xz, dragPos.xz);

            // Reset handle position back to center (UpdateHandleDragPosition moved it to drag point)
            EntityManager.SetComponentData(handleEntity,
                                           new NT_HandlePosition {
                                               Position = center,
                                               Rotation = quaternion.identity
                                           });

            // Update circle component: fix center and apply new radius
            var circle = EntityManager.GetComponentData<NT_HandleCircle>(handleEntity);
            circle.Center = center;
            circle.Radius = newRadius;
            EntityManager.SetComponentData(handleEntity, circle);

            return newRadius;
        }

        /// <summary>
        ///     Handles dragging for position-based handles.
        ///     Computes the delta from the previous config position, updates the config,
        ///     propagates movement to child handles, and syncs their config positions.
        /// </summary>
        /// <param name="handle">The position handle entity being dragged.</param>
        /// <param name="key">The handle key from <see cref="NT_HandleLink.Key"/>.</param>
        /// <param name="handlePos">The handle's current world position.</param>
        protected void HandlePositionDrag(Entity handle, int key, float3 handlePos) {
            var previousPos = GetHandleConfigPosition(key);
            var delta = handlePos - previousPos;

            ApplyHandleConfigPosition(key, handlePos);

            if (math.lengthsq(delta) > 0f) {
                PropagateToChildren(handle, delta);
                SyncChildConfigPositions(handle);
            }
        }

        /// <summary>
        ///     Syncs config positions for all children of the given parent handle
        ///     by reading each child's current position and writing it to the config
        ///     via <see cref="ApplyHandleConfigPosition"/>.
        /// </summary>
        /// <param name="parentHandle">The parent handle whose children should be synced.</param>
        protected void SyncChildConfigPositions(Entity parentHandle) {
            for (var i = 0; i < m_Handles.Length; i++) {
                var child = m_Handles[i];
                if (!EntityManager.HasComponent<NT_HandleParent>(child)) {
                    continue;
                }

                var parentComponent = EntityManager.GetComponentData<NT_HandleParent>(child);
                if (parentComponent.Parent != parentHandle) {
                    continue;
                }

                var childLink = EntityManager.GetComponentData<NT_HandleLink>(child);
                var childPos  = EntityManager.GetComponentData<NT_HandlePosition>(child).Position;
                ApplyHandleConfigPosition(childLink.Key, childPos);
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

            var camera = Camera.main;
            if (camera == null) return false;

            var mousePos = Mouse.current.position.ReadValue();
            var ray      = camera.ScreenPointToRay(mousePos);

            // Plane equation: y = planeY
            if (math.abs(ray.direction.y) < 0.0001f) return false;

            var t = (planeY - ray.origin.y) / ray.direction.y;
            if (t < 0) return false;

            intersection = (float3)ray.origin + (float3)ray.direction * t;
            return true;
        }

        /// <summary>
        ///     Gets the intersection point of the camera ray with a YZ plane at the specified X.
        /// </summary>
        protected bool TryGetYZPlaneIntersection(float planeX, out float3 intersection) {
            intersection = float3.zero;

            var camera = Camera.main;
            if (camera == null) return false;

            var mousePos = Mouse.current.position.ReadValue();
            var ray      = camera.ScreenPointToRay(mousePos);

            if (math.abs(ray.direction.x) < 0.0001f) return false;

            var t = (planeX - ray.origin.x) / ray.direction.x;
            if (t < 0) return false;

            intersection = (float3)ray.origin + (float3)ray.direction * t;
            return true;
        }

        /// <summary>
        ///     Gets the intersection point of the camera ray with a XY plane at the specified Z.
        /// </summary>
        protected bool TryGetXYPlaneIntersection(float planeZ, out float3 intersection) {
            intersection = float3.zero;

            var camera = Camera.main;
            if (camera == null) return false;

            var mousePos = Mouse.current.position.ReadValue();
            var ray      = camera.ScreenPointToRay(mousePos);

            if (math.abs(ray.direction.z) < 0.0001f) return false;

            var t = (planeZ - ray.origin.z) / ray.direction.z;
            if (t < 0) return false;

            intersection = (float3)ray.origin + (float3)ray.direction * t;
            return true;
        }

        /// <summary>
        ///     Gets the closest point on an axis line from the camera ray.
        /// </summary>
        protected bool TryGetAxisIntersection(float3 axisOrigin, float3 axisDir, out float3 intersection) {
            intersection = axisOrigin;

            var camera = Camera.main;
            if (camera == null) return false;

            var mousePos  = Mouse.current.position.ReadValue();
            var ray       = camera.ScreenPointToRay(mousePos);
            var rayOrigin = (float3)ray.origin;
            var rayDir    = (float3)ray.direction;

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
        ///     Returns the current config position for a handle key.
        ///     Override in derived tools to map handle keys to config fields.
        /// </summary>
        /// <param name="key">The handle key from <see cref="NT_HandleLink.Key"/>.</param>
        /// <returns>The position stored in the tool's config, or <see cref="float3.zero"/> if unmapped.</returns>
        protected virtual float3 GetHandleConfigPosition(int key) {
            return float3.zero;
        }

        /// <summary>
        ///     Writes a position to the config field identified by the handle key.
        ///     Override in derived tools to map handle keys to config fields.
        /// </summary>
        /// <param name="key">The handle key from <see cref="NT_HandleLink.Key"/>.</param>
        /// <param name="position">The new position value.</param>
        protected virtual void ApplyHandleConfigPosition(int key, float3 position) {
            // Override in derived tools
        }

        /// <summary>
        ///     Called when a handle drag operation starts.
        /// </summary>
        /// <param name="handle">The handle entity being dragged.</param>
        protected virtual void OnHandleDragStart(Entity handle) {
            // Override in derived tools
        }

        /// <summary>
        ///     Called each frame while dragging a handle.
        /// </summary>
        /// <param name="handle">The handle entity being dragged.</param>
        protected virtual void OnHandleDragging(Entity handle) {
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

            // Continue dragging
            UpdateHandleDragPosition(m_DraggedHandle);
            OnHandleDragging(m_DraggedHandle);
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
