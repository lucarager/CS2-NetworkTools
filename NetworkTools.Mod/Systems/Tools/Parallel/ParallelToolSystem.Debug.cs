namespace NetworkTools.Systems.Tools.Parallel {
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Partial class containing the test hooks, called from a debugger or a test scenario.
    ///     A test is <see cref="DebugSelectNear" />, parameter writes, <see cref="RequestApply" />.
    ///     The queries are built from the entity manager, not <c>SystemAPI</c>.
    ///     The source generator rewrites the latter under the usings of another file of the class.
    /// </summary>
    public partial class NT_ParallelToolSystem {
        /// <summary>
        ///     Selects the path between the nodes nearest to two points.
        ///     It does what hovering the first and clicking both would.
        /// </summary>
        /// <param name="start">A point near the first node.</param>
        /// <param name="end">A point near the last node.</param>
        public void DebugSelectNear(float3 start, float3 end) {
            var nodes = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Node, ConnectedEdge>()
                .WithNone<Deleted, Temp>()
                .Build(EntityManager)
                .ToEntityArray(Allocator.Temp);
            var first = Entity.Null;
            var last  = Entity.Null;

            for (var i = 0; i < nodes.Length; i++) {
                var position = EntityManager.GetComponentData<Node>(nodes[i]).m_Position;

                if (first == Entity.Null
                    || math.distance(position, start) < DistanceTo(first, start)) {
                    first = nodes[i];
                }

                if (last == Entity.Null || math.distance(position, end) < DistanceTo(last, end)) {
                    last = nodes[i];
                }
            }

            nodes.Dispose();
            ResetToIdle();
            HandleNoHover();
            HandleAddNode(first);
            HandlePathUpdate(new ControlPoint { m_OriginalEntity = last });
            HandleAddNode(last);

            m_UpdateNeeded = true;
        }

        /// <summary>
        ///     Gives the copy a road prefab by name.
        ///     Without one the copy takes the source's.
        /// </summary>
        /// <param name="road">Name of the road prefab ("Small Road").</param>
        public void DebugPrefab(string road) {
            var prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();

            prefabs.TryGetPrefab(new PrefabID(nameof(RoadPrefab), road), out var prefab);
            NetPrefab.Set(prefab, prefabs.GetEntity(prefab), Entity.Null);

            m_UpdateNeeded = true;
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
