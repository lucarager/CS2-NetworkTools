// <copyright file="NT_MarkerLink.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components {
    #region Using Statements

    using Unity.Entities;

    #endregion

    public struct NT_MarkerLink : IComponentData {
        public Entity LinkedEntity;
        public int Key;
    }
}