namespace NetworkTools.Systems.Tools.RoadShape {
    using System.Text;

    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Simulation;
    using Game.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Partial class containing the test hooks, called from a debugger or a test scenario.
    ///     A test is <see cref="DebugSelectNear" />, parameter writes, <see cref="RequestApply" />.
    /// </summary>
    public partial class NT_RoadShapeToolSystem {
        /// <summary>
        ///     Selects the path between the nodes nearest to two points.
        ///     It does what hovering the first and clicking both would.
        /// </summary>
        /// <param name="start">A point near the first node.</param>
        /// <param name="end">A point near the last node.</param>
        public void DebugSelectNear(float3 start, float3 end) {
            var first = NearestNode(start);
            var last  = NearestNode(end);

            ResetToIdle();
            HandleNoHover();
            HandleAddNode(first);
            HandlePathUpdate(new ControlPoint { m_OriginalEntity = last });
            HandleAddNode(last);

            m_UpdateNeeded = true;
        }

        /// <summary>
        ///     Finds the built node nearest to a point.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>The node.</returns>
        private Entity NearestNode(float3 point) {
            var nodes = SystemAPI.QueryBuilder()
                .WithAll<Node, ConnectedEdge>()
                .WithNone<Deleted, Temp>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            var nearest = Entity.Null;

            for (var i = 0; i < nodes.Length; i++) {
                var position = EntityManager.GetComponentData<Node>(nodes[i]).m_Position;

                if (nearest == Entity.Null
                    || math.distance(position, point) < DistanceTo(nearest, point)) {
                    nearest = nodes[i];
                }
            }

            nodes.Dispose();

            return nearest;
        }

        /// <summary>
        ///     Measures the ground over a straight line on the map's own heights, every few metres.
        ///     A measure holds the distance, x, z, then the cover over a 16 m width.
        ///     The cover is read at the left edge, the centre, and the right edge.
        /// </summary>
        /// <param name="a">Start of the line.</param>
        /// <param name="b">End of the line.</param>
        /// <param name="step">Distance between two measures.</param>
        /// <returns>The lines, one per measure.</returns>
        public string DebugCover(float3 a, float3 b, float step) {
            var held    = m_MapHeights.IsCreated;
            var terrain = MapTerrain();
            var length  = math.distance(a, b);
            var text    = new StringBuilder();
            var side    = new float3 { xz = math.normalize(MathUtils.Right((b - a).xz)) * 8f };

            for (var d = 0f; d <= length; d += step) {
                var p = math.lerp(a, b, d / length);
                var l = TerrainUtils.SampleHeight(ref terrain, p - side) - p.y;
                var c = TerrainUtils.SampleHeight(ref terrain, p) - p.y;
                var r = TerrainUtils.SampleHeight(ref terrain, p + side) - p.y;

                text.AppendLine($"{d:F1} ({p.x:F1}, {p.z:F1}) [{l:F1} {c:F1} {r:F1}]");
            }

            if (!held) {
                ReleaseMapHeights();
            }

            return text.ToString();
        }

        /// <summary>
        ///     Measures the distance from a node to a point.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="point">The point.</param>
        /// <returns>The distance.</returns>
        private float DistanceTo(Entity node, float3 point) {
            return math.distance(EntityManager.GetComponentData<Node>(node).m_Position, point);
        }
    }
}
