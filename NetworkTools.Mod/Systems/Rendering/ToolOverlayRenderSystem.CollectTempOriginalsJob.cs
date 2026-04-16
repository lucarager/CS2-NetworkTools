namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Game.Tools;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_ToolOverlayRenderSystem {
        /// <summary>
        ///     Collects the <see cref="Temp.m_Original"/> entities from temp edges
        ///     so the draw job can skip those originals when rendering eligible edges.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct CollectTempOriginalsJob : IJobChunk {
            [ReadOnly] public required ComponentTypeHandle<Temp> m_TempComponentTypeHandle;

            public NativeParallelHashSet<Entity>.ParallelWriter m_TempOriginals;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var tempArray = chunk.GetNativeArray(ref m_TempComponentTypeHandle);

                for (var i = 0; i < tempArray.Length; i++) {
                    var original = tempArray[i].m_Original;
                    if (original != Entity.Null) {
                        m_TempOriginals.Add(original);
                    }
                }
            }
        }
    }
}
