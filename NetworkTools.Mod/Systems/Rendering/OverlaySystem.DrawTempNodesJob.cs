namespace NetworkTools.Systems {
    using LucaModsCommon.Rendering;
    using System.Diagnostics.CodeAnalysis;
    using Game.Net;
    using Game.Rendering;
    using Game.Tools;
    using NetworkTools.Systems.Rendering;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    public partial class NT_OverlaySystem {
        /// <summary>
        ///     Job to draw temp node overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if USE_BURST
        [BurstCompile]
#endif
        protected struct DrawTempNodesJob : IJobChunk {
            [ReadOnly] public required CustomOverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required RenderColors m_Colors;
            [ReadOnly] public required RenderDimensions m_Dimensions;
            [ReadOnly] public required ComponentTypeHandle<Temp> m_TempComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Node> m_NodeComponentTypeHandle;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var nodesArray = chunk.GetNativeArray(ref m_NodeComponentTypeHandle);
                var tempArray = chunk.GetNativeArray(ref m_TempComponentTypeHandle);

                for (var i = 0; i < nodesArray.Length; i++) {
                    var node = nodesArray[i];
                    var temp = tempArray[i];

                    // Only render nodes with Replace flag set
                    if ((temp.m_Flags & TempFlags.Replace) == 0) {
                        continue;
                    }

                    // Draw a simple 3f wide white dot
                    var color = (Color)(Vector4)m_Colors.TempNode;
                    m_Buffer.DrawCircle(color,
                        color,
                        0.1f,
                        0,
                        new float2(0, 1),
                        node.m_Position,
                        3f);
                }
            }
        }
    }
}
