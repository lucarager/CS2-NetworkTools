// <copyright file="NT_BaseToolSystem.Jobs.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Colossal.Mathematics;
    using Game.Areas;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Node = Game.Net.Node;
    using SubArea = Game.Prefabs.SubArea;
    using SubNet = Game.Prefabs.SubNet;
    using SubObject = Game.Prefabs.SubObject;

    #endregion

    public abstract partial class NT_BaseToolSystem {
        public enum ToolMode {
            None,
            AddNodeToEdge,
            RemoveNodeFromEdge,
            CombineNodes,
            ConnectNodes,
        }

        public struct CreateNetDefinitionsJob : IJob {
            [ReadOnly] private ToolMode                               m_ToolMode;
            [ReadOnly] private NativeList<ControlPoint>               m_ControlPoints;
            [ReadOnly] private ComponentLookup<Edge>                  m_EdgeLookup;
            [ReadOnly] private ComponentLookup<Node>                  m_NodeLookup;
            [ReadOnly] private ComponentLookup<Curve>                 m_CurveLookup;
            [ReadOnly] private ComponentLookup<PrefabRef>             m_PrefabRefLookup;
            [ReadOnly] private ComponentLookup<Attachment>            m_AttachmentLookup;
            [ReadOnly] private ComponentLookup<Owner>                 m_OwnerLookup;
            [ReadOnly] private ComponentLookup<NetGeometryData>       m_NetGeometryDataLookup;
            [ReadOnly] private ComponentLookup<SpawnableObjectData>   m_SpawnableObjectDataLookup;
            [ReadOnly] private ComponentLookup<Building>              m_BuildingLookup;
            [ReadOnly] private ComponentLookup<Extension>             m_ExtensionLookup;
            [ReadOnly] private ComponentLookup<AreaGeometryData>      m_AreaGeometryDataLookup;
            [ReadOnly] private ComponentLookup<Transform>             m_TransformLookup;
            [ReadOnly] private ComponentLookup<LocalTransformCache>   m_LocalTransformCacheLookup;
            [ReadOnly] private BufferLookup<Game.Net.SubNet>          m_SubNetLookup;
            [ReadOnly] private BufferLookup<Game.Areas.SubArea>       m_SubAreaLookup;
            [ReadOnly] private BufferLookup<Game.Areas.Node>          m_AreaNodeLookup;
            [ReadOnly] private BufferLookup<LocalNodeCache>           m_CachedNodeLookup;
            [ReadOnly] private BufferLookup<SubNet>                   m_PrefabSubNetLookup;
            [ReadOnly] private BufferLookup<SubArea>                  m_PrefabSubAreaLookup;
            [ReadOnly] private BufferLookup<SubAreaNode>              m_PrefabSubAreaNodeLookup;
            [ReadOnly] private BufferLookup<PlaceholderObjectElement> m_PlaceholderElementLookup;
            [ReadOnly] private BufferLookup<ConnectedEdge>            m_ConnectedEdges;
            [ReadOnly] private BufferLookup<SubObject>                m_SubObjectLookup;
            [ReadOnly] private Entity                                 m_Edge;
            [ReadOnly] private Entity                                 m_NetPrefab;
            [ReadOnly] private RandomSeed                             m_RandomSeed;
            [ReadOnly] private bool                                   m_LeftHandTraffic;
            private            EntityCommandBuffer                    m_CommandBuffer;

            public void Execute() {
                // General Validation

                // Owner hashmap
                var nativeParallelHashMap = default(NativeParallelHashMap<Entity, OwnerDefinition>);

                switch (m_ToolMode) {
                    case ToolMode.AddNodeToEdge:
                        HandleAddNodeToEdge(ref nativeParallelHashMap);
                        // Logic for adding node to edge
                        break;
                    case ToolMode.RemoveNodeFromEdge:
                        HandleRemoveNodeFromEdge();
                        // Logic for adding node to edge
                        break;
                    case ToolMode.CombineNodes:
                        HandleCombineNodes();
                        // Logic for adding node to edge
                        break;
                    case ToolMode.ConnectNodes:
                        HandleConnectNodes();
                        // Logic for adding node to edge
                        break;
                    case ToolMode.None:
                    default:
                        break;
                }
            }

            private void HandleAddNodeToEdge(ref NativeParallelHashMap<Entity, OwnerDefinition> ownerDefinitions) {
                // Validate correct request
                if (m_Edge == Entity.Null || m_ControlPoints.Length != 1) {
                    return;
                }

                var controlPoint = m_ControlPoints[0];
                var entity       = m_CommandBuffer.CreateEntity();
                var random       = m_RandomSeed.GetRandom(0);

                var creationDefinition = default(CreationDefinition);
                creationDefinition.m_Prefab     =  m_NetPrefab;
                creationDefinition.m_RandomSeed =  random.NextInt();
                creationDefinition.m_Flags      |= CreationFlags.SubElevation;

                var netCourse = default(NetCourse);
                netCourse.m_Curve         = new Bezier4x3(controlPoint.m_Position, controlPoint.m_Position, controlPoint.m_Position, controlPoint.m_Position);
                netCourse.m_StartPosition = GetCoursePos(netCourse.m_Curve, controlPoint, 0f);
                netCourse.m_EndPosition   = GetCoursePos(netCourse.m_Curve, controlPoint, 1f);
                netCourse.m_StartPosition.m_Flags = netCourse.m_StartPosition.m_Flags | CoursePosFlags.IsFirst | CoursePosFlags.IsLast |
                                                    CoursePosFlags.IsRight            | CoursePosFlags.IsLeft;
                netCourse.m_EndPosition.m_Flags = netCourse.m_EndPosition.m_Flags | CoursePosFlags.IsFirst | CoursePosFlags.IsLast | CoursePosFlags.IsRight |
                                                  CoursePosFlags.IsLeft;
                netCourse.m_Length     = MathUtils.Length(netCourse.m_Curve);
                netCourse.m_FixedIndex = -1;

                if (GetOwnerDefinition(ref ownerDefinitions, Entity.Null, true, netCourse.m_StartPosition, netCourse.m_EndPosition, out var ownerDefinition)) {
                    m_CommandBuffer.AddComponent(entity, ownerDefinition);
                    if (GetLocalCurve(netCourse, ownerDefinition, out var localCurveCache)) {
                        m_CommandBuffer.AddComponent(entity, localCurveCache);
                    }
                } else {
                    netCourse.m_StartPosition.m_ParentMesh = -1;
                    netCourse.m_EndPosition.m_ParentMesh   = -1;
                }

                m_CommandBuffer.AddComponent(entity, netCourse);
                m_CommandBuffer.AddComponent(entity, creationDefinition);
                m_CommandBuffer.AddComponent(entity, default(Updated));
            }

            private void HandleRemoveNodeFromEdge() {
                if (m_Edge == Entity.Null || m_ControlPoints.Length == 0) {
                    return;
                }

                var nodeToRemove = m_ControlPoints[0];

                // Logic for removing node from edge
                // This would involve:
                // - Validating the node is removable
                // - Finding connected edges
                // - Merging or deleting edges as appropriate
                // - Updating the network structure
            }

            private void HandleCombineNodes() {
                if (m_ControlPoints.Length < 2) { }

                // Logic for combining nodes
                // This would involve:
                // - Merging the two nodes
                // - Redirecting all connected edges
                // - Removing duplicate connections
                // - Cleaning up orphaned entities
            }

            private void HandleConnectNodes() {
                if (m_ControlPoints.Length < 2) {
                    return;
                }

                var startNode = m_ControlPoints[0];
                var endNode   = m_ControlPoints[m_ControlPoints.Length - 1];

                // Logic for connecting nodes
                // This would involve:
                // - Validating both nodes exist and are compatible
                // - Creating connecting edge(s)
                // - Updating composition if necessary
                // - Recording transaction for undo/redo
            }

            private bool GetOwnerDefinition(ref NativeParallelHashMap<Entity, OwnerDefinition> ownerDefinitions, Entity original, bool checkControlPoints,
                                            CoursePos startPos, CoursePos endPos, out OwnerDefinition ownerDefinition) {
                var entity = Entity.Null;
                ownerDefinition = default;
                Owner owner;

                if (m_OwnerLookup.TryGetComponent(original, out owner)) {
                    entity = owner.m_Owner;
                }

                OwnerDefinition ownerDefinition2;
                Transform       transform;
                Curve           curve;
                if (ownerDefinitions.IsCreated && ownerDefinitions.TryGetValue(entity, out ownerDefinition2)) {
                    ownerDefinition = ownerDefinition2;
                } else if (m_TransformLookup.TryGetComponent(entity, out transform)) {
                    var entity3 = Entity.Null;
                    if (m_OwnerLookup.TryGetComponent(entity, out owner)) {
                        entity3 = owner.m_Owner;
                    }

                    UpdateOwnerObject(entity3, entity, Entity.Null, transform);
                    ownerDefinition.m_Prefab   = m_PrefabRefLookup[entity].m_Prefab;
                    ownerDefinition.m_Position = transform.m_Position;
                    ownerDefinition.m_Rotation = transform.m_Rotation;
                    Attachment attachment;
                    Transform  transform2;
                    if (m_AttachmentLookup.TryGetComponent(entity, out attachment) &&
                        m_TransformLookup.TryGetComponent(attachment.m_Attached, out transform2)) {
                        UpdateOwnerObject(Entity.Null, attachment.m_Attached, entity, transform2);
                    }

                    if (!ownerDefinitions.IsCreated) {
                        ownerDefinitions = new NativeParallelHashMap<Entity, OwnerDefinition>(8, Allocator.Temp);
                    }

                    ownerDefinitions.Add(entity, ownerDefinition);
                } else if (m_CurveLookup.TryGetComponent(entity, out curve)) {
                    ownerDefinition.m_Prefab   = m_PrefabRefLookup[entity].m_Prefab;
                    ownerDefinition.m_Position = curve.m_Bezier.a;
                    ownerDefinition.m_Rotation = new float4(curve.m_Bezier.d, 0f);
                }

                if ((startPos.m_Flags & endPos.m_Flags & (CoursePosFlags.IsFirst | CoursePosFlags.IsLast)) !=
                    (CoursePosFlags.IsFirst | CoursePosFlags.IsLast) && m_SubObjectLookup.HasBuffer(m_NetPrefab)) {
                    var dynamicBuffer2        = m_SubObjectLookup[m_NetPrefab];
                    var nativeParallelHashMap = default(NativeParallelHashMap<Entity, int>);
                    for (var j = 0; j < dynamicBuffer2.Length; j++) {
                        var subObject = dynamicBuffer2[j];
                        if ((subObject.m_Flags & SubObjectFlags.MakeOwner) != 0) {
                            var courseObjectTransform = GetCourseObjectTransform(subObject, startPos, endPos);
                            CreateCourseObject(subObject.m_Prefab, courseObjectTransform, ownerDefinition, ref nativeParallelHashMap);
                            ownerDefinition.m_Prefab   = subObject.m_Prefab;
                            ownerDefinition.m_Position = courseObjectTransform.m_Position;
                            ownerDefinition.m_Rotation = courseObjectTransform.m_Rotation;
                            break;
                        }
                    }

                    for (var k = 0; k < dynamicBuffer2.Length; k++) {
                        var subObject2 = dynamicBuffer2[k];
                        if ((subObject2.m_Flags & (SubObjectFlags.CoursePlacement | SubObjectFlags.MakeOwner)) == SubObjectFlags.CoursePlacement) {
                            var courseObjectTransform2 = GetCourseObjectTransform(subObject2, startPos, endPos);
                            CreateCourseObject(subObject2.m_Prefab, courseObjectTransform2, ownerDefinition, ref nativeParallelHashMap);
                        }
                    }

                    if (nativeParallelHashMap.IsCreated) {
                        nativeParallelHashMap.Dispose();
                    }
                }

                return ownerDefinition.m_Prefab != Entity.Null;
            }

            private bool GetLocalCurve(NetCourse course, OwnerDefinition ownerDefinition, out LocalCurveCache localCurveCache) {
                var transform = ObjectUtils.InverseTransform(new Transform(ownerDefinition.m_Position, ownerDefinition.m_Rotation));
                localCurveCache           = default;
                localCurveCache.m_Curve.a = ObjectUtils.WorldToLocal(transform, course.m_Curve.a);
                localCurveCache.m_Curve.b = ObjectUtils.WorldToLocal(transform, course.m_Curve.b);
                localCurveCache.m_Curve.c = ObjectUtils.WorldToLocal(transform, course.m_Curve.c);
                localCurveCache.m_Curve.d = ObjectUtils.WorldToLocal(transform, course.m_Curve.d);
                return true;
            }

            private Transform GetCourseObjectTransform(SubObject subObject, CoursePos startPos, CoursePos endPos) {
                var       coursePos = (subObject.m_Flags & SubObjectFlags.StartPlacement) != 0 ? startPos : endPos;
                Transform transform;
                transform.m_Position = ObjectUtils.LocalToWorld(coursePos.m_Position, coursePos.m_Rotation, subObject.m_Position);
                transform.m_Rotation = math.mul(coursePos.m_Rotation, subObject.m_Rotation);
                return transform;
            }

            private void CreateCourseObject(Entity                                 prefab, Transform transform, OwnerDefinition ownerDefinition,
                                            ref NativeParallelHashMap<Entity, int> selectedSpawnables) {
                var entity             = m_CommandBuffer.CreateEntity();
                var creationDefinition = default(CreationDefinition);
                creationDefinition.m_Prefab = prefab;
                var objectDefinition = default(ObjectDefinition);
                objectDefinition.m_ParentMesh = -1;
                objectDefinition.m_Position   = transform.m_Position;
                objectDefinition.m_Rotation   = transform.m_Rotation;
                if (ownerDefinition.m_Prefab != Entity.Null) {
                    var transform2 = ObjectUtils.WorldToLocal(
                        ObjectUtils.InverseTransform(new Transform(ownerDefinition.m_Position, ownerDefinition.m_Rotation)),
                        transform);
                    objectDefinition.m_LocalPosition = transform2.m_Position;
                    objectDefinition.m_LocalRotation = transform2.m_Rotation;
                    m_CommandBuffer.AddComponent(entity, ownerDefinition);
                } else {
                    objectDefinition.m_LocalPosition = transform.m_Position;
                    objectDefinition.m_LocalRotation = transform.m_Rotation;
                }

                m_CommandBuffer.AddComponent(entity, creationDefinition);
                m_CommandBuffer.AddComponent(entity, objectDefinition);
                m_CommandBuffer.AddComponent(entity, default(Updated));
                CreateSubNets(transform, prefab);
                CreateSubAreas(transform, prefab, ref selectedSpawnables);
            }

            private void CreateSubAreas(Transform transform, Entity prefab, ref NativeParallelHashMap<Entity, int> selectedSpawnables) {
                if (m_PrefabSubAreaLookup.HasBuffer(prefab)) {
                    var dynamicBuffer  = m_PrefabSubAreaLookup[prefab];
                    var dynamicBuffer2 = m_PrefabSubAreaNodeLookup[prefab];
                    var random         = m_RandomSeed.GetRandom(10000);
                    var i              = 0;
                    while (i < dynamicBuffer.Length) {
                        var subArea = dynamicBuffer[i];
                        int num;
                        if (!m_PlaceholderElementLookup.HasBuffer(subArea.m_Prefab)) {
                            num = random.NextInt();
                            goto IL_00C2;
                        }

                        var dynamicBuffer3 = m_PlaceholderElementLookup[subArea.m_Prefab];
                        if (!selectedSpawnables.IsCreated) {
                            selectedSpawnables = new NativeParallelHashMap<Entity, int>(10, Allocator.Temp);
                        }

                        if (AreaUtils.SelectAreaPrefab(
                                dynamicBuffer3,
                                m_SpawnableObjectDataLookup,
                                selectedSpawnables,
                                ref random,
                                out subArea.m_Prefab,
                                out num)) {
                            goto IL_00C2;
                        }

                        IL_02BF:
                        i++;
                        continue;
                        IL_00C2:
                        var ptr                = m_AreaGeometryDataLookup[subArea.m_Prefab];
                        var entity             = m_CommandBuffer.CreateEntity();
                        var creationDefinition = default(CreationDefinition);
                        creationDefinition.m_Prefab     = subArea.m_Prefab;
                        creationDefinition.m_RandomSeed = num;
                        if (ptr.m_Type != AreaType.Lot) {
                            creationDefinition.m_Flags |= CreationFlags.Hidden;
                        }

                        m_CommandBuffer.AddComponent(entity, creationDefinition);
                        m_CommandBuffer.AddComponent(entity, default(Updated));
                        m_CommandBuffer.AddComponent(
                            entity,
                            new OwnerDefinition {
                                m_Prefab   = prefab,
                                m_Position = transform.m_Position,
                                m_Rotation = transform.m_Rotation,
                            });
                        var dynamicBuffer4 = m_CommandBuffer.AddBuffer<Game.Areas.Node>(entity);
                        dynamicBuffer4.ResizeUninitialized(subArea.m_NodeRange.y - subArea.m_NodeRange.x + 1);
                        var dynamicBuffer5 = default(DynamicBuffer<LocalNodeCache>);


                        var num2 = ObjectToolBaseSystem.GetFirstNodeIndex(dynamicBuffer2, subArea.m_NodeRange);
                        var num3 = 0;
                        for (var j = subArea.m_NodeRange.x; j <= subArea.m_NodeRange.y; j++) {
                            var position   = dynamicBuffer2[num2].m_Position;
                            var @float     = ObjectUtils.LocalToWorld(transform, position);
                            var parentMesh = dynamicBuffer2[num2].m_ParentMesh;
                            var num4       = math.select(float.MinValue, position.y, parentMesh >= 0);
                            dynamicBuffer4[num3] = new Game.Areas.Node(@float, num4);
                            num3++;
                            if (++num2 == subArea.m_NodeRange.y) {
                                num2 = subArea.m_NodeRange.x;
                            }
                        }

                        goto IL_02BF;
                    }
                }
            }

            private void CreateSubNets(Transform transform, Entity prefab) {
                if (m_SubNetLookup.HasBuffer(prefab)) {
                    var dynamicBuffer = m_PrefabSubNetLookup[prefab];
                    var nativeList    = new NativeList<float4>(dynamicBuffer.Length * 2, Allocator.Temp);
                    for (var i = 0; i < dynamicBuffer.Length; i++) {
                        var subNet = dynamicBuffer[i];
                        if (subNet.m_NodeIndex.x >= 0) {
                            while (nativeList.Length <= subNet.m_NodeIndex.x) {
                                var @float = default(float4);
                                nativeList.Add(in @float);
                            }

                            ref var ptr = ref nativeList;
                            var     num = subNet.m_NodeIndex.x;
                            ptr[num] += new float4(subNet.m_Curve.a, 1f);
                        }

                        if (subNet.m_NodeIndex.y >= 0) {
                            while (nativeList.Length <= subNet.m_NodeIndex.y) {
                                var @float = default(float4);
                                nativeList.Add(in @float);
                            }

                            ref var ptr = ref nativeList;
                            var     num = subNet.m_NodeIndex.y;
                            ptr[num] += new float4(subNet.m_Curve.d, 1f);
                        }
                    }

                    for (var j = 0; j < nativeList.Length; j++) {
                        ref var ptr = ref nativeList;
                        var     num = j;
                        ptr[num] /= math.max(1f, nativeList[j].w);
                    }

                    for (var k = 0; k < dynamicBuffer.Length; k++) {
                        var subNet2 = NetUtils.GetSubNet(dynamicBuffer, k, m_LeftHandTraffic, ref m_NetGeometryDataLookup);
                        var entity  = m_CommandBuffer.CreateEntity();
                        m_CommandBuffer.AddComponent(
                            entity,
                            new CreationDefinition {
                                m_Prefab = subNet2.m_Prefab,
                            });
                        m_CommandBuffer.AddComponent(entity, default(Updated));
                        m_CommandBuffer.AddComponent(
                            entity,
                            new OwnerDefinition {
                                m_Prefab   = prefab,
                                m_Position = transform.m_Position,
                                m_Rotation = transform.m_Rotation,
                            });
                        var netCourse = default(NetCourse);
                        netCourse.m_Curve                       = TransformCurve(subNet2.m_Curve, transform.m_Position, transform.m_Rotation);
                        netCourse.m_StartPosition.m_Position    = netCourse.m_Curve.a;
                        netCourse.m_StartPosition.m_Rotation    = NetUtils.GetNodeRotation(MathUtils.StartTangent(netCourse.m_Curve), transform.m_Rotation);
                        netCourse.m_StartPosition.m_CourseDelta = 0f;
                        netCourse.m_StartPosition.m_Elevation   = subNet2.m_Curve.a.y;
                        netCourse.m_StartPosition.m_ParentMesh  = subNet2.m_ParentMesh.x;
                        if (subNet2.m_NodeIndex.x >= 0) {
                            var @float = nativeList[subNet2.m_NodeIndex.x];
                            netCourse.m_StartPosition.m_Position = ObjectUtils.LocalToWorld(transform, @float.xyz);
                        }

                        netCourse.m_EndPosition.m_Position    = netCourse.m_Curve.d;
                        netCourse.m_EndPosition.m_Rotation    = NetUtils.GetNodeRotation(MathUtils.EndTangent(netCourse.m_Curve), transform.m_Rotation);
                        netCourse.m_EndPosition.m_CourseDelta = 1f;
                        netCourse.m_EndPosition.m_Elevation   = subNet2.m_Curve.d.y;
                        netCourse.m_EndPosition.m_ParentMesh  = subNet2.m_ParentMesh.y;
                        if (subNet2.m_NodeIndex.y >= 0) {
                            var @float = nativeList[subNet2.m_NodeIndex.y];
                            netCourse.m_EndPosition.m_Position = ObjectUtils.LocalToWorld(transform, @float.xyz);
                        }

                        netCourse.m_Length                = MathUtils.Length(netCourse.m_Curve);
                        netCourse.m_FixedIndex            = -1;
                        netCourse.m_StartPosition.m_Flags = netCourse.m_StartPosition.m_Flags | CoursePosFlags.IsFirst;
                        netCourse.m_EndPosition.m_Flags   = netCourse.m_EndPosition.m_Flags   | CoursePosFlags.IsLast;
                        if (netCourse.m_StartPosition.m_Position.Equals(netCourse.m_EndPosition.m_Position)) {
                            netCourse.m_StartPosition.m_Flags = netCourse.m_StartPosition.m_Flags | CoursePosFlags.IsLast;
                            netCourse.m_EndPosition.m_Flags   = netCourse.m_EndPosition.m_Flags   | CoursePosFlags.IsFirst;
                        }

                        m_CommandBuffer.AddComponent(entity, netCourse);
                        if (subNet2.m_Upgrades != default) {
                            var upgraded = new Upgraded {
                                m_Flags = subNet2.m_Upgrades,
                            };
                            m_CommandBuffer.AddComponent(entity, upgraded);
                        }
                    }

                    nativeList.Dispose();
                }
            }

            private Bezier4x3 TransformCurve(Bezier4x3 curve, float3 position, quaternion rotation) {
                curve.a = ObjectUtils.LocalToWorld(position, rotation, curve.a);
                curve.b = ObjectUtils.LocalToWorld(position, rotation, curve.b);
                curve.c = ObjectUtils.LocalToWorld(position, rotation, curve.c);
                curve.d = ObjectUtils.LocalToWorld(position, rotation, curve.d);
                return curve;
            }

            private CoursePos GetCoursePos(Bezier4x3 curve, ControlPoint controlPoint, float courseDelta) {
                var coursePos = default(CoursePos);
                if (controlPoint.m_OriginalEntity != Entity.Null) {
                    if (m_EdgeLookup.HasComponent(controlPoint.m_OriginalEntity)) {
                        if (controlPoint.m_CurvePosition <= 0f) {
                            coursePos.m_Entity        = m_EdgeLookup[controlPoint.m_OriginalEntity].m_Start;
                            coursePos.m_SplitPosition = 0f;
                        } else if (controlPoint.m_CurvePosition >= 1f) {
                            coursePos.m_Entity        = m_EdgeLookup[controlPoint.m_OriginalEntity].m_End;
                            coursePos.m_SplitPosition = 1f;
                        } else {
                            coursePos.m_Entity        = controlPoint.m_OriginalEntity;
                            coursePos.m_SplitPosition = controlPoint.m_CurvePosition;
                        }
                    } else if (m_NodeLookup.HasComponent(controlPoint.m_OriginalEntity)) {
                        coursePos.m_Entity        = controlPoint.m_OriginalEntity;
                        coursePos.m_SplitPosition = controlPoint.m_CurvePosition;
                    }
                }

                coursePos.m_Position    = controlPoint.m_Position;
                coursePos.m_Elevation   = controlPoint.m_Elevation;
                coursePos.m_Rotation    = NetUtils.GetNodeRotation(MathUtils.Tangent(curve, courseDelta));
                coursePos.m_CourseDelta = courseDelta;
                coursePos.m_ParentMesh  = controlPoint.m_ElementIndex.x;
                var entity = controlPoint.m_OriginalEntity;
                while (m_OwnerLookup.HasComponent(entity) && !m_BuildingLookup.HasComponent(entity) && !m_ExtensionLookup.HasComponent(entity)) {
                    Edge                edge;
                    LocalTransformCache localTransformCache;
                    LocalTransformCache localTransformCache2;
                    if (m_LocalTransformCacheLookup.HasComponent(entity)) {
                        coursePos.m_ParentMesh = m_LocalTransformCacheLookup[entity].m_ParentMesh;
                    } else if (m_EdgeLookup.TryGetComponent(entity, out edge)                                     &&
                               m_LocalTransformCacheLookup.TryGetComponent(edge.m_Start, out localTransformCache) &&
                               m_LocalTransformCacheLookup.TryGetComponent(edge.m_End, out localTransformCache2)) {
                        coursePos.m_ParentMesh = math.select(
                            localTransformCache.m_ParentMesh,
                            -1,
                            localTransformCache.m_ParentMesh != localTransformCache2.m_ParentMesh);
                    }

                    entity = m_OwnerLookup[entity].m_Owner;
                }

                return coursePos;
            }

            private void UpdateOwnerObject(Entity owner, Entity original, Entity attachedParent, Transform transform) {
                var entity             = m_CommandBuffer.CreateEntity();
                var prefab             = m_PrefabRefLookup[original].m_Prefab;
                var creationDefinition = default(CreationDefinition);
                creationDefinition.m_Owner    =  owner;
                creationDefinition.m_Original =  original;
                creationDefinition.m_Flags    |= CreationFlags.Upgrade | CreationFlags.Parent;
                var objectDefinition = default(ObjectDefinition);
                objectDefinition.m_ParentMesh = -1;
                objectDefinition.m_Position   = transform.m_Position;
                objectDefinition.m_Rotation   = transform.m_Rotation;
                if (m_TransformLookup.HasComponent(owner)) {
                    var transform2 = ObjectUtils.WorldToLocal(ObjectUtils.InverseTransform(m_TransformLookup[owner]), transform);
                    objectDefinition.m_LocalPosition = transform2.m_Position;
                    objectDefinition.m_LocalRotation = transform2.m_Rotation;
                } else {
                    objectDefinition.m_LocalPosition = transform.m_Position;
                    objectDefinition.m_LocalRotation = transform.m_Rotation;
                }

                PrefabRef prefabRef;
                if (m_PrefabRefLookup.TryGetComponent(attachedParent, out prefabRef)) {
                    creationDefinition.m_Attached =  prefabRef.m_Prefab;
                    creationDefinition.m_Flags    |= CreationFlags.Attach;
                }

                m_CommandBuffer.AddComponent(entity, creationDefinition);
                m_CommandBuffer.AddComponent(entity, objectDefinition);
                m_CommandBuffer.AddComponent(entity, default(Updated));
                UpdateSubNets(transform, prefab, original);
                UpdateSubAreas(transform, prefab, original);
            }

            private bool HasEdgeStartOrEnd(Entity node, Entity owner) {
                var dynamicBuffer = m_ConnectedEdges[node];
                for (var i = 0; i < dynamicBuffer.Length; i++) {
                    var edge  = dynamicBuffer[i].m_Edge;
                    var edge2 = m_EdgeLookup[edge];
                    if ((edge2.m_Start == node || edge2.m_End == node) && m_OwnerLookup.HasComponent(edge) && m_OwnerLookup[edge].m_Owner == owner) {
                        return true;
                    }
                }

                return false;
            }

            private void UpdateSubNets(Transform transform, Entity prefab, Entity original) {
                var nativeParallelHashSet = default(NativeParallelHashSet<Entity>);

                if (m_SubNetLookup.HasBuffer(original)) {
                    var dynamicBuffer2 = m_SubNetLookup[original];
                    for (var k = 0; k < dynamicBuffer2.Length; k++) {
                        var subNet = dynamicBuffer2[k].m_SubNet;
                        if (m_NodeLookup.HasComponent(subNet)) {
                            if (!HasEdgeStartOrEnd(subNet, original)) {
                                var node               = m_NodeLookup[subNet];
                                var entity             = m_CommandBuffer.CreateEntity();
                                var creationDefinition = default(CreationDefinition);
                                creationDefinition.m_Original = subNet;
                                //if (this.m_EditorContainerData.HasComponent(subNet)) {
                                //    creationDefinition.m_SubPrefab = this.m_EditorContainerData[subNet].m_Prefab;
                                //}
                                m_CommandBuffer.AddComponent(
                                    entity,
                                    new OwnerDefinition {
                                        m_Prefab   = prefab,
                                        m_Position = transform.m_Position,
                                        m_Rotation = transform.m_Rotation,
                                    });
                                m_CommandBuffer.AddComponent(entity, creationDefinition);
                                m_CommandBuffer.AddComponent(entity, default(Updated));
                                var netCourse = default(NetCourse);
                                netCourse.m_Curve                       = new Bezier4x3(node.m_Position, node.m_Position, node.m_Position, node.m_Position);
                                netCourse.m_Length                      = 0f;
                                netCourse.m_FixedIndex                  = -1;
                                netCourse.m_StartPosition.m_Entity      = subNet;
                                netCourse.m_StartPosition.m_Position    = node.m_Position;
                                netCourse.m_StartPosition.m_Rotation    = node.m_Rotation;
                                netCourse.m_StartPosition.m_CourseDelta = 0f;
                                netCourse.m_EndPosition.m_Entity        = subNet;
                                netCourse.m_EndPosition.m_Position      = node.m_Position;
                                netCourse.m_EndPosition.m_Rotation      = node.m_Rotation;
                                netCourse.m_EndPosition.m_CourseDelta   = 1f;
                                m_CommandBuffer.AddComponent(entity, netCourse);
                            }
                        } else if (m_EdgeLookup.HasComponent(subNet) && (!nativeParallelHashSet.IsCreated || !nativeParallelHashSet.Contains(subNet))) {
                            var edge3               = m_EdgeLookup[subNet];
                            var entity2             = m_CommandBuffer.CreateEntity();
                            var creationDefinition2 = default(CreationDefinition);
                            creationDefinition2.m_Original = subNet;
                            //if (this.m_EditorContainerData.HasComponent(subNet)) {
                            //    creationDefinition2.m_SubPrefab = this.m_EditorContainerData[subNet].m_Prefab;
                            //}
                            m_CommandBuffer.AddComponent(
                                entity2,
                                new OwnerDefinition {
                                    m_Prefab   = prefab,
                                    m_Position = transform.m_Position,
                                    m_Rotation = transform.m_Rotation,
                                });
                            m_CommandBuffer.AddComponent(entity2, creationDefinition2);
                            m_CommandBuffer.AddComponent(entity2, default(Updated));
                            var netCourse2 = default(NetCourse);
                            netCourse2.m_Curve                       = m_CurveLookup[subNet].m_Bezier;
                            netCourse2.m_Length                      = MathUtils.Length(netCourse2.m_Curve);
                            netCourse2.m_FixedIndex                  = -1;
                            netCourse2.m_StartPosition.m_Entity      = edge3.m_Start;
                            netCourse2.m_StartPosition.m_Position    = netCourse2.m_Curve.a;
                            netCourse2.m_StartPosition.m_Rotation    = NetUtils.GetNodeRotation(MathUtils.StartTangent(netCourse2.m_Curve));
                            netCourse2.m_StartPosition.m_CourseDelta = 0f;
                            netCourse2.m_EndPosition.m_Entity        = edge3.m_End;
                            netCourse2.m_EndPosition.m_Position      = netCourse2.m_Curve.d;
                            netCourse2.m_EndPosition.m_Rotation      = NetUtils.GetNodeRotation(MathUtils.EndTangent(netCourse2.m_Curve));
                            netCourse2.m_EndPosition.m_CourseDelta   = 1f;
                            m_CommandBuffer.AddComponent(entity2, netCourse2);
                        }
                    }
                }

                if (nativeParallelHashSet.IsCreated) {
                    nativeParallelHashSet.Dispose();
                }
            }

            private void UpdateSubAreas(Transform transform, Entity prefab, Entity original) {
                if (m_SubAreaLookup.HasBuffer(original)) {
                    var dynamicBuffer = m_SubAreaLookup[original];
                    for (var i = 0; i < dynamicBuffer.Length; i++) {
                        var area               = dynamicBuffer[i].m_Area;
                        var entity             = m_CommandBuffer.CreateEntity();
                        var creationDefinition = default(CreationDefinition);
                        creationDefinition.m_Original = area;
                        m_CommandBuffer.AddComponent(
                            entity,
                            new OwnerDefinition {
                                m_Prefab   = prefab,
                                m_Position = transform.m_Position,
                                m_Rotation = transform.m_Rotation,
                            });
                        m_CommandBuffer.AddComponent(entity, creationDefinition);
                        m_CommandBuffer.AddComponent(entity, default(Updated));
                        var dynamicBuffer2 = m_AreaNodeLookup[area];
                        m_CommandBuffer.AddBuffer<Game.Areas.Node>(entity).CopyFrom(dynamicBuffer2.AsNativeArray());
                        if (m_CachedNodeLookup.HasBuffer(area)) {
                            var dynamicBuffer3 = m_CachedNodeLookup[area];
                            m_CommandBuffer.AddBuffer<LocalNodeCache>(entity).CopyFrom(dynamicBuffer3.AsNativeArray());
                        }
                    }
                }
            }
        }
    }
}