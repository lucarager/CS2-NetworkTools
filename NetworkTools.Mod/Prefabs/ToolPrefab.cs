// <copyright file="NT_ToolPrefab.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Prefabs {
    using System.Collections.Generic;
    using Colossal.UI.Binding;
    using Unity.Entities;

    public class NT_ToolPrefab : PrefabBase, IJsonWritable {
        public string Id;
        public string DisplayName;
        public string Description;
        public string Icon;
        public string Category;
        public bool   Active;
        public int    Index;

        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            base.GetPrefabComponents(components);
            components.Add(ComponentType.ReadWrite<NetworkTools.Components.NT_ToolData>());
        }

        /// <inheritdoc />
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);

            writer.PropertyName("Id");
            writer.Write(Id);

            writer.PropertyName("DisplayName");
            writer.Write(DisplayName);

            writer.PropertyName("Icon");
            writer.Write(Icon);

            writer.PropertyName("Description");
            writer.Write(Description);

            writer.PropertyName("Category");
            writer.Write(Category);

            writer.PropertyName("Active");
            writer.Write(Active);

            writer.PropertyName("Index");
            writer.Write(Index);

            writer.PropertyName("PrefabId");
            writer.Write(GetPrefabID().GetName());

            writer.TypeEnd();
        }
    }
}
