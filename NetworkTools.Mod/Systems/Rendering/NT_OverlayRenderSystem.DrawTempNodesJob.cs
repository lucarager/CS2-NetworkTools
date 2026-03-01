namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Game.Net;
    using Game.Rendering;
    using Game.Tools;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    public partial class NT_OverlayRenderSystem {
        /// <summary>
        ///     Job to draw temp node overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawTempNodesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer m_Buffer;
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

                    // Use node position, lifted slightly so it shows over other elements
                    var position = node.m_Position;
                    position.y += 1f;

                    // Draw a simple 3f wide white dot
                    var color = new Color(1f, 1f, 1f, 1f);
                    m_Buffer.DrawCircle(color,
                        color,
                        0.1f,
                        0,
                        new float2(0, 1),
                        position,
                        3f);
                }
            }
        }
    }
}
