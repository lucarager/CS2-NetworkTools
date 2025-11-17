// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace Game.Prefabs {
    public class NT_ToolPrefab : PrefabBase {
        public string DisplayName;
        public string Description;
        public string Icon;

        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            base.GetPrefabComponents(components);
            components.Add(ComponentType.ReadWrite<NT_ToolLookup>());
        }
    }
}
