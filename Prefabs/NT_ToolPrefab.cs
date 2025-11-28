// <copyright file="NT_ToolPrefab.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Prefabs {
    #region Using Statements

    using System.Collections.Generic;
    using Unity.Entities;

    #endregion

    public class NT_ToolPrefab : PrefabBase {
        public string DisplayName;
        public string Description;
        public string Icon;

        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            base.GetPrefabComponents(components);
            components.Add(ComponentType.ReadWrite<NT_ToolData>());
        }
    }
}