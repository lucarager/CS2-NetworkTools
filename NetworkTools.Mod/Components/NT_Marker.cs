// <copyright file="NT_MarkerLink.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components {
    using System;
    #region Using Statements

    using Unity.Entities;

    #endregion


    [Flags]
    public enum MarkerTypeFlags : uint {
        None = 0,
        BezierPoint = 1 << 0,    
        BezierStartPoint = 1 << 1,   
        BezierEndPoint = 1 << 2,
        BezierControlPoint = 1 << 3,
        Curve = 1 << 4,
    }

    public struct NT_Marker : IComponentData {
        public MarkerTypeFlags TypeFlags;
    }
}