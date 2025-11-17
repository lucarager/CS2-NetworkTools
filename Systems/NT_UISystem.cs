using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.Prefabs;
using NetworkTools.Extensions;
using UnityEngine.Diagnostics;

// <copyright file="P_UISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    using Colossal.IO.AssetLookupbase;
    using Colossal.Logging;
    using Colossal.UI;
    using Colossal.UI.Binding;
    using Game.Tools;
    using NetworkTools.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_UISystem : ExtendedUISystemBase {
        private ValueBindingHelper<ToolUILookup[]> m_ToolLookupBinding;
        private EntityQuery                      m_ToolPrefabQuery;
        private ToolSystem                       m_ToolSystem;
        private PrefabSystem                     m_PrefabSystem;
        private PrefixedLogger                   m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_UISystem));
            m_Log.Debug("OnCreate()");

            m_ToolLookupBinding = CreateBinding("UI_DATA", new ToolUILookup[] { });
            CreateTrigger<string>("SELECT_TOOL", HandleSelectTool);

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            m_ToolPrefabQuery = SystemAPI.QueryBuilder()
                .WithAll<NT_ToolLookup>()
                .Build();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities     = m_ToolPrefabQuery.ToEntityArray(Allocator.Temp);
            var toolLookupArray = new ToolUILookup[entities.Length];
            for (var i = 0; i < entities.Length; i++) {
                var prefab = m_PrefabSystem.GetPrefab<NT_ToolPrefab>(entities[i]);
                toolLookupArray[i] = new ToolUILookup(prefab);
            }
            m_ToolLookupBinding.Value = toolLookupArray;
        }

        private void HandleSelectTool(string id) {
            m_Log.Debug($"HandleSelectTool(id: {id})");

            if (m_PrefabSystem.TryGetPrefab(
                    new PrefabID(
                        "NT_ToolPrefab",
                        id),
                    out var prefab)) {
                m_ToolSystem.ActivatePrefabTool(prefab);
            }
        }

        /// <summary>
        /// Struct to store and send Zone Lookup and to the React UI.
        /// </summary>
        public readonly struct ToolUILookup : IJsonWritable {
            private readonly NT_ToolPrefab m_Prefab;

            public ToolUILookup(NT_ToolPrefab prefab) {
                m_Prefab = prefab;
            }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("DisplayName");
                writer.Write(m_Prefab.DisplayName);

                writer.PropertyName("Icon");
                writer.Write(m_Prefab.Icon);

                writer.PropertyName("ID");
                writer.Write(m_Prefab.GetPrefabID().GetName());

                writer.TypeEnd();
            }
        }
    }
}