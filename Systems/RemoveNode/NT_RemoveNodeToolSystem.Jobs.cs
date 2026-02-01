// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using System;
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using Color = UnityEngine.Color;

    #endregion

    public partial class NT_RemoveNodeToolSystem {
#if BURST
        [BurstCompile]
#endif
            /// <summary>
            /// Creates definitions for Entities from query.
            /// </summary>
            private struct CreateDefinitionJob : IJob {
            [ReadOnly] public required NativeReference<Entity>           ControlPoint;
            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required SlopeCurveConfig                  CurveConfig;
            [ReadOnly] public required TerrainHeightData                 TerrainHeight;
            [ReadOnly] public required OverlayRenderSystem.Buffer        RenderBuffer;
            public required            EntityCommandBuffer               ECB;

            public void Execute() {

            }
        }
    }
}