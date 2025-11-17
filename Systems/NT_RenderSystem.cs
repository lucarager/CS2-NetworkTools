// <copyright file="RenderSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
namespace NetworkTools.Systems {
    using Game;
    using Game.Rendering;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using NetworkTools.Utils;

    /// <summary>
    /// Overlay Rendering System.
    /// </summary>
    public partial class NT_RenderSystem : GameSystemBase {
        // Systems & References
        private OverlayRenderSystem m_OverlayRenderSystem;
        private PreCullingSystem m_PreCullingSystem;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(NT_RenderSystem));
            m_Log.Debug("OnCreate()");


            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_PreCullingSystem = World.GetOrCreateSystemManaged<PreCullingSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
           
        }

        /// <summary>
        /// Job to draw parcel overlays.
        /// </summary>
        protected struct DrawOverlaysJob : IJobChunk {
            [ReadOnly] private OverlayRenderSystem.Buffer m_OverlayRenderBuffer;
            [ReadOnly] private NativeList<PreCullingLookup> m_CullingLookup;

            public DrawOverlaysJob(
                OverlayRenderSystem.Buffer overlayRenderBuffer,
                NativeList<PreCullingLookup> cullingLookup
            ) {
                m_OverlayRenderBuffer = overlayRenderBuffer;
                m_CullingLookup = cullingLookup;
            }

            /// <inheritdoc/>
            public void Execute(
                    in ArchetypeChunk chunk,
                    int unfilteredChunkIndex,
                    bool useEnabledMask,
                    in v128 chunkEnabledMask) {
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            }

            private bool IsNearCamera(CullingInfo cullingInfo) {
                return cullingInfo.m_CullingIndex != 0 &&
                       (m_CullingLookup[cullingInfo.m_CullingIndex].m_Flags &
                        PreCullingFlags.NearCamera) > 0U;
            }
        }
    }
}