// <copyright file="NT_AddDelete.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Prefabs {
    #region Using Statements

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Marks an entity as selected with its position in the selection path.
    /// PathIndex indicates the order of traversal (0 = start, increasing towards end).
    /// </summary>
    public struct NT_Selected : IComponentData {
        public int PathIndex;
    }
}