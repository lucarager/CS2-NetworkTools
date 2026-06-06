namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Game.Net;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Systems.Rendering;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_ToolOverlayRenderSystem {
        /// <summary>
        ///     Sequential job that iterates RemoveNode node chunks and renders
        ///     eligible and temp-deleted nodes directly to the overlay buffer.
        ///     Visibility is pre-determined by <see cref="FrustumCullEntitiesJob"/>.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if USE_BURST
        [BurstCompile]
#endif
        protected struct RenderRemoveNodeOverlayJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle              m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Node>     m_NodeComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Temp>     m_TempComponentTypeHandle;
            [ReadOnly] public required NativeParallelHashSet<Entity> m_VisibleEntities;

            public required CustomOverlayRenderSystem.Buffer m_Buffer;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                if (chunk.Has(ref m_TempComponentTypeHandle)) {
                    RenderTempNodes(in chunk);
                } else {
                    RenderEligibleNodes(in chunk);
                }
            }

            private void RenderEligibleNodes(in ArchetypeChunk chunk) {
                var entities   = chunk.GetNativeArray(m_EntityTypeHandle);
                var nodesArray = chunk.GetNativeArray(ref m_NodeComponentTypeHandle);

                for (var i = 0; i < entities.Length; i++) {
                    if (!m_VisibleEntities.Contains(entities[i])) {
                        continue;
                    }

                    m_Buffer.DrawCircle(NT_Colors.NodeVisible.Rest.Fill, nodesArray[i].m_Position, 3f);
                }
            }

            private void RenderTempNodes(in ArchetypeChunk chunk) {
                var nodesArray = chunk.GetNativeArray(ref m_NodeComponentTypeHandle);
                var tempArray  = chunk.GetNativeArray(ref m_TempComponentTypeHandle);

                for (var i = 0; i < nodesArray.Length; i++) {
                    if ((tempArray[i].m_Flags & TempFlags.Delete) == 0) {
                        continue;
                    }

                    m_Buffer.DrawCircle(NT_Colors.NodeRemoved.Rest.Fill, nodesArray[i].m_Position, 3f);
                }
            }
        }
    }
}
