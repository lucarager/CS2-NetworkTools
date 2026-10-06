namespace NetworkTools.Tests {
    using System.Collections.Generic;

    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Reads what a tool previewed or built on the <see cref="ProvingGround" />.
    /// </summary>
    internal static class NetworkReader {
        /// <summary>
        ///     A node where a tunnel starts or ends: one tunnel edge joins it.
        /// </summary>
        public struct Mouth {
            /// <summary>
            ///     The position of the node.
            /// </summary>
            public float3 Position;

            /// <summary>
            ///     Direction of the tunnel edge at the node.
            /// </summary>
            public float3 Tangent;
        }

        /// <summary>
        ///     Gets the tunnels of a road laid along a lane, as distances from the lane's start.
        ///     Tunnel edges that follow one another count as one tunnel.
        /// </summary>
        /// <param name="entityManager">The entity manager of the world.</param>
        /// <param name="lane">The lane.</param>
        /// <param name="across">Distance of the road to the left of the lane's middle.</param>
        /// <param name="preview">True to read the preview, false to read what is built.</param>
        /// <returns>The tunnels, in order.</returns>
        public static List<Bounds1> Tunnels(
            EntityManager      entityManager,
            ProvingGround.Lane lane,
            float              across,
            bool               preview) {
            var edges   = TunnelEdges(entityManager, preview);
            var origin  = ProvingGround.Point(lane, 0f, across);
            var tunnels = new List<Bounds1>();

            for (var i = 0; i < edges.Count; i++) {
                var bezier = entityManager.GetComponentData<Curve>(edges[i]).m_Bezier;

                if (math.abs(bezier.a.z - origin.z) > 1f || math.abs(bezier.d.z - origin.z) > 1f) {
                    continue;
                }

                tunnels.Add(new Bounds1(
                    math.min(bezier.a.x, bezier.d.x) - origin.x,
                    math.max(bezier.a.x, bezier.d.x) - origin.x));
            }

            tunnels.Sort((a, b) => a.min.CompareTo(b.min));

            for (var i = tunnels.Count - 1; i > 0; i--) {
                if (tunnels[i].min - tunnels[i - 1].max > 0.01f) {
                    continue;
                }

                tunnels[i - 1] = new Bounds1(tunnels[i - 1].min, tunnels[i].max);
                tunnels.RemoveAt(i);
            }

            return tunnels;
        }

        /// <summary>
        ///     Gets the mouths within an area, wherever the network goes.
        ///     A node with one tunnel edge is a mouth, whatever else joins it.
        ///     The tunnel edges of the whole map are counted, so that a long one is not missed.
        ///     A preview may hold two nodes at one place: they count as one.
        /// </summary>
        /// <param name="entityManager">The entity manager of the world.</param>
        /// <param name="area">The area, in x and z.</param>
        /// <param name="preview">True to read the preview, false to read what is built.</param>
        /// <returns>The mouths, in order of x then z.</returns>
        public static List<Mouth> Mouths(EntityManager entityManager, Bounds2 area, bool preview) {
            var edges  = TunnelEdges(entityManager, preview);
            var joined = new Dictionary<int2, int>();
            var found  = new Dictionary<int2, Mouth>();

            for (var i = 0; i < edges.Count; i++) {
                var bezier = entityManager.GetComponentData<Curve>(edges[i]).m_Bezier;

                Count(joined, found, bezier.a, MathUtils.StartTangent(bezier));
                Count(joined, found, bezier.d, MathUtils.EndTangent(bezier));
            }

            var mouths = new List<Mouth>();

            foreach (var pair in found) {
                if (joined[pair.Key] == 1 && MathUtils.Intersect(area, pair.Value.Position.xz)) {
                    mouths.Add(pair.Value);
                }
            }

            mouths.Sort((a, b) => a.Position.x != b.Position.x
                ? a.Position.x.CompareTo(b.Position.x)
                : a.Position.z.CompareTo(b.Position.z));

            return mouths;
        }

        /// <summary>
        ///     Gets the built nodes of a road along a lane, as distances from the lane's start.
        /// </summary>
        /// <param name="entityManager">The entity manager of the world.</param>
        /// <param name="lane">The lane.</param>
        /// <param name="across">Distance of the road to the left of the lane's middle.</param>
        /// <returns>The nodes, in order.</returns>
        public static List<float> Nodes(
            EntityManager      entityManager,
            ProvingGround.Lane lane,
            float              across) {
            return NodesAlong(
                entityManager,
                ProvingGround.Point(lane, 0f, across),
                ProvingGround.Point(lane, ProvingGround.LaneLength, across));
        }

        /// <summary>
        ///     Gets the built nodes on a straight road, as distances from its start.
        /// </summary>
        /// <param name="entityManager">The entity manager of the world.</param>
        /// <param name="a">Start of the road.</param>
        /// <param name="d">End of the road.</param>
        /// <returns>The nodes within a metre of the road, in order.</returns>
        public static List<float> NodesAlong(EntityManager entityManager, float3 a, float3 d) {
            var query = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<Node, ConnectedEdge>()
                        .WithNone<Deleted, Temp>()
                        .Build(entityManager);

            var found = query.ToEntityArray(Allocator.Temp);
            var road  = new Line2.Segment(a.xz, d.xz);
            var nodes = new List<float>();

            for (var i = 0; i < found.Length; i++) {
                var position = entityManager.GetComponentData<Node>(found[i]).m_Position;

                if (MathUtils.Distance(road, position.xz, out var t) <= 1f) {
                    nodes.Add(t * math.distance(a.xz, d.xz));
                }
            }

            found.Dispose();
            query.Dispose();
            nodes.Sort();

            return nodes;
        }

        /// <summary>
        ///     Gets the height of the built edge nearest to a point, at that point.
        /// </summary>
        /// <param name="entityManager">The entity manager of the world.</param>
        /// <param name="point">The point, in x and z.</param>
        /// <returns>The height of the edge's curve.</returns>
        public static float EdgeHeight(EntityManager entityManager, float2 point) {
            var query = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<Edge, Curve>()
                        .WithNone<Deleted, Temp>()
                        .Build(entityManager);

            var edges   = query.ToEntityArray(Allocator.Temp);
            var nearest = float.MaxValue;
            var height  = float.NaN;

            for (var i = 0; i < edges.Length; i++) {
                var bezier   = entityManager.GetComponentData<Curve>(edges[i]).m_Bezier;
                var distance = MathUtils.Distance(bezier.xz, point, out var t);

                if (distance < nearest) {
                    nearest = distance;
                    height  = MathUtils.Position(bezier, t).y;
                }
            }

            edges.Dispose();
            query.Dispose();

            return height;
        }

        /// <summary>
        ///     Gets the ends of the built edges with an end within an area, as coordinates.
        ///     Each edge gives its start and its end, x, y, and z, in order of x then z.
        /// </summary>
        /// <param name="entityManager">The entity manager of the world.</param>
        /// <param name="area">The area, in x and z.</param>
        /// <returns>The coordinates.</returns>
        public static List<float> Ends(EntityManager entityManager, Bounds2 area) {
            var query = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<Edge, Curve>()
                        .WithNone<Deleted, Temp>()
                        .Build(entityManager);

            var edges = query.ToEntityArray(Allocator.Temp);
            var ends  = new List<float3>();

            for (var i = 0; i < edges.Length; i++) {
                var bezier = entityManager.GetComponentData<Curve>(edges[i]).m_Bezier;

                var inside = MathUtils.Intersect(area, bezier.a.xz)
                             || MathUtils.Intersect(area, bezier.d.xz);

                if (inside) {
                    ends.Add(bezier.a);
                    ends.Add(bezier.d);
                }
            }

            edges.Dispose();
            query.Dispose();
            ends.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));

            var coordinates = new List<float>();

            for (var i = 0; i < ends.Count; i++) {
                coordinates.Add(ends[i].x);
                coordinates.Add(ends[i].y);
                coordinates.Add(ends[i].z);
            }

            return coordinates;
        }

        /// <summary>
        ///     Gets a number that changes whenever an edge changes, the preview's included.
        ///     It takes in each edge's entity, ends, composition, and preview flags.
        ///     The sum of the edges' hashes keeps it from depending on their order.
        /// </summary>
        /// <param name="entityManager">The entity manager of the world.</param>
        /// <returns>The number.</returns>
        public static uint Fingerprint(EntityManager entityManager) {
            var query = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<Edge, Curve>()
                        .Build(entityManager);

            var edges = query.ToEntityArray(Allocator.Temp);
            var sum   = (uint)edges.Length;

            for (var i = 0; i < edges.Length; i++) {
                var edge   = edges[i];
                var bezier = entityManager.GetComponentData<Curve>(edge).m_Bezier;
                var hash   = math.hash(new float3x2(bezier.a, bezier.d))
                             ^ math.hash(new int2(edge.Index, edge.Version));

                if (entityManager.HasComponent<Composition>(edge)) {
                    var composition = entityManager.GetComponentData<Composition>(edge).m_Edge;

                    hash = hash * 31 + math.hash(new int2(composition.Index, composition.Version));
                }

                if (entityManager.HasComponent<Temp>(edge)) {
                    hash = hash * 31 + (uint)entityManager.GetComponentData<Temp>(edge).m_Flags;
                }

                sum += hash;
            }

            edges.Dispose();
            query.Dispose();

            return sum;
        }

        /// <summary>
        ///     Gets the tunnel edges of the map, the preview's or the built ones.
        ///     The preview holds the edges it replaces too, marked for deletion: they are left out.
        /// </summary>
        /// <param name="entityManager">The entity manager of the world.</param>
        /// <param name="preview">True to read the preview, false to read what is built.</param>
        /// <returns>The edges.</returns>
        private static List<Entity> TunnelEdges(EntityManager entityManager, bool preview) {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                          .WithAll<Edge, Curve, Composition>()
                          .WithNone<Deleted>();

            builder = preview ? builder.WithAll<Temp>() : builder.WithNone<Temp>();

            var query = builder.Build(entityManager);
            var edges = query.ToEntityArray(Allocator.Temp);
            var found = new List<Entity>();

            for (var i = 0; i < edges.Length; i++) {
                if (preview && (entityManager.GetComponentData<Temp>(edges[i]).m_Flags
                                & (TempFlags.Delete | TempFlags.Hidden)) != 0) {
                    continue;
                }

                var composition = entityManager.GetComponentData<Composition>(edges[i]).m_Edge;
                var data        = entityManager.GetComponentData<NetCompositionData>(composition);

                if ((data.m_Flags.m_General & CompositionFlags.General.Tunnel) != 0) {
                    found.Add(edges[i]);
                }
            }

            edges.Dispose();
            query.Dispose();

            return found;
        }

        /// <summary>
        ///     Counts a tunnel edge on the node at one of its ends, known by its place to the cm.
        /// </summary>
        /// <param name="joined">Tunnel edges counted at each place, updated.</param>
        /// <param name="found">The mouth each place would be, updated.</param>
        /// <param name="position">The end of the edge.</param>
        /// <param name="tangent">Direction of the edge there.</param>
        private static void Count(
            Dictionary<int2, int>   joined,
            Dictionary<int2, Mouth> found,
            float3                  position,
            float3                  tangent) {
            var place = (int2)math.round(position.xz * 100f);

            joined.TryGetValue(place, out var count);

            joined[place] = count + 1;
            found[place]  = new Mouth {
                Position = position,
                Tangent  = tangent,
            };
        }
    }
}
