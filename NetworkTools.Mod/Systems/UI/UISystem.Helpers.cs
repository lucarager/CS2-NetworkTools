namespace NetworkTools.Systems.UI {
    using Colossal.Entities;
    using Game.Net;
    using Unity.Entities;
    using Colossal.UI.Binding;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using NetworkTools.Extensions;
    using NetworkTools.Settings;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Connect;
    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Utils;
    using Unity.Entities;
    using Colossal.UI.Binding;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using NetworkTools.Extensions;
    using NetworkTools.Settings;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Connect;
    using NetworkTools.Systems.Tools.Generate;
    using NetworkTools.Systems.Tools.Parallel;
    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Utils;
    using Unity.Entities;

    public partial class NT_UISystem {
        private string GetComputedNodeName(Entity nodeEntity, int fallbackIndex) {
            if (TryGetNodeName(nodeEntity, out var streetName)) {
                return $"Node on {streetName}";
            }
            return $"Node {fallbackIndex + 1}";
        }

        private bool TryGetNodeName(Entity nodeEntity, out string name) {

            if (EntityManager.TryGetBuffer<ConnectedEdge>(nodeEntity, true, out var connectedEdges)) {
                // For now, get the first connected edge's name as the node name.
                // todo handle intersections.
                name = m_NameSystem.GetRenderedLabelName(connectedEdges[0].m_Edge);
                return true;
            }

            name = "Node";
            return false;
        }

        private SelectedEntityType DetermineEntityType(Entity entity) {
            if (EntityManager.HasComponent<Edge>(entity)) {
                return SelectedEntityType.Edge;
            }

            if (EntityManager.HasComponent<Node>(entity)) {
                return SelectedEntityType.Node;
            }

            return SelectedEntityType.Unknown;
        }

        private int ComputeSelectionHash(Entity[] entities) {
            unchecked {
                var hash = 17;
                for (var i = 0; i < entities.Length; i++) {
                    hash = hash * 31 + entities[i].Index;
                    hash = hash * 31 + entities[i].Version;
                }

                return hash;
            }
        }
    }
}
