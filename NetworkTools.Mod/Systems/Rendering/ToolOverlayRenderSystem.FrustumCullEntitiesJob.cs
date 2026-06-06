namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Rendering;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using FrustumPlanes = Game.Rendering.FrustumPlanes;

    public partial class NT_ToolOverlayRenderSystem {
        /// <summary>
        ///     Parallel pre-pass that frustum- and distance-culls edges and nodes,
        ///     writing visible entities into a shared hash set consumed by prepare jobs.
        ///     <para>
        ///         Edges are culled by their <see cref="Curve"/> bezier AABB.
        ///         Nodes are culled by their <see cref="Node"/> position (zero-extent AABB).
        ///     </para>
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if USE_BURST
        [BurstCompile]
#endif
        protected struct FrustumCullEntitiesJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle             m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Curve>   m_CurveComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Node>    m_NodeComponentTypeHandle;
            [ReadOnly] public required NativeArray<FrustumPlanes.PlanePacket4> m_CullingPlanes;
            [ReadOnly] public required float3                       m_CameraPosition;
            [ReadOnly] public required float                        m_MaxDistance;

            public NativeParallelHashSet<Entity>.ParallelWriter m_VisibleEntities;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var entities = chunk.GetNativeArray(m_EntityTypeHandle);

                // Branch once per chunk: edges have Curve, nodes have Node
                if (chunk.Has(ref m_CurveComponentTypeHandle)) {
                    CullEdges(in chunk, entities);
                } else if (chunk.Has(ref m_NodeComponentTypeHandle)) {
                    CullNodes(in chunk, entities);
                }
            }

            /// <summary>
            ///     Culls edges by computing the AABB of their bezier curve.
            /// </summary>
            private void CullEdges(in ArchetypeChunk chunk, NativeArray<Entity> entities) {
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);

                for (var i = 0; i < entities.Length; i++) {
                    var bounds  = MathUtils.Bounds(curvesArray[i].m_Bezier);
                    var center  = (bounds.min + bounds.max) * 0.5f;
                    var extents = (bounds.max - bounds.min) * 0.5f;

                    if (IsVisible(center, extents)) {
                        m_VisibleEntities.Add(entities[i]);
                    }
                }
            }

            /// <summary>
            ///     Culls nodes by their point position (zero-extent AABB).
            /// </summary>
            private void CullNodes(in ArchetypeChunk chunk, NativeArray<Entity> entities) {
                var nodesArray = chunk.GetNativeArray(ref m_NodeComponentTypeHandle);

                for (var i = 0; i < entities.Length; i++) {
                    if (IsVisible(nodesArray[i].m_Position, float3.zero)) {
                        m_VisibleEntities.Add(entities[i]);
                    }
                }
            }

            /// <summary>
            ///     Combined frustum + distance visibility test.
            /// </summary>
            private bool IsVisible(float3 center, float3 extents) {
                // Distance cull (squared to avoid sqrt)
                if (m_MaxDistance > 0f && math.distancesq(center, m_CameraPosition) > m_MaxDistance) {
                    return false;
                }

                // Frustum cull (skip if no planes available, e.g. Camera.main was null)
                if (m_CullingPlanes.Length == 0) {
                    return true;
                }

                return IsInsideFrustum(m_CullingPlanes, center, extents);
            }

            /// <summary>
            ///     Vectorized AABB-vs-frustum test using SOA plane packets.
            /// </summary>
            private static bool IsInsideFrustum(NativeArray<FrustumPlanes.PlanePacket4> planePackets,
                                                float3 center, float3 extents) {
                var cx       = center.xxxx;
                var cy       = center.yyyy;
                var cz       = center.zzzz;
                var ex       = extents.xxxx;
                var ey       = extents.yyyy;
                var ez       = extents.zzzz;
                var outCount = (int4)0;

                for (var i = 0; i < planePackets.Length; i++) {
                    var p      = planePackets[i];
                    var dist   = p.Xs * cx + p.Ys * cy + p.Zs * cz + p.Distances;
                    var radius = math.abs(p.Xs) * ex + math.abs(p.Ys) * ey + math.abs(p.Zs) * ez;
                    outCount  += (int4)(dist + radius < 0f);
                }

                return math.csum(outCount) == 0;
            }
        }
    }
}
