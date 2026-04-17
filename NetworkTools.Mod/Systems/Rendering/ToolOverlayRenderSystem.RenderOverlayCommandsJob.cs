namespace NetworkTools.Systems {
    using NetworkTools.Systems.Rendering;
    using Unity.Collections;
    using Unity.Jobs;

    public partial class NT_ToolOverlayRenderSystem {
        /// <summary>
        ///     Sequential job that reads pre-computed <see cref="OverlayDrawCommand"/>s from a
        ///     <see cref="NativeStream"/> and dispatches them to the overlay buffer.
        /// </summary>
#if BURST
        [BurstCompile]
#endif
        protected struct RenderOverlayCommandsJob : IJob {
            [ReadOnly] public required CustomOverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required NativeStream.Reader              m_CommandReader;
                       public required int                              m_ForEachCount;

            public void Execute() {
                for (var foreachIndex = 0; foreachIndex < m_ForEachCount; foreachIndex++) {
                    var remaining = m_CommandReader.BeginForEachIndex(foreachIndex);

                    while (remaining > 0) {
                        var cmd = m_CommandReader.Read<OverlayDrawCommand>();
                        remaining--;

                        switch (cmd.m_Type) {
                            case OverlayCommandType.Curve:
                                m_Buffer.DrawCurve(cmd.m_Color, cmd.m_Bezier, cmd.m_Width, cmd.m_ForceUp);
                                break;

                            case OverlayCommandType.Line:
                                m_Buffer.DrawLine(cmd.m_Color,
                                    new Colossal.Mathematics.Line3.Segment(cmd.m_PointA, cmd.m_PointB),
                                    cmd.m_Width);
                                break;

                            case OverlayCommandType.Circle:
                                m_Buffer.DrawCircle(cmd.m_Color, cmd.m_PointA, cmd.m_Width);
                                break;
                        }
                    }

                    m_CommandReader.EndForEachIndex();
                }
            }
        }
    }
}
