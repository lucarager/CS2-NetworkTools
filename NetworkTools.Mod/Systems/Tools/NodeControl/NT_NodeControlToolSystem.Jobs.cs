// <copyright file="NT_NodeControlToolSystem.Jobs.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    public partial class NT_NodeControlToolSystem {
#if BURST
        [BurstCompile]
#endif
        /// <summary>
        ///     Creates temp marker objects at specific positions.
        /// </summary>
        internal struct CreateMarkersJob : IJob {
            [ReadOnly] public required NativeArray<float3> Positions;
            [ReadOnly] public required Entity MarkerPrefab;
            public required EntityCommandBuffer ECB;

            public void Execute() {
                for (var i = 0; i < Positions.Length; i++) {
                    var position = Positions[i];
                    var entity = ECB.CreateEntity();

                    var creationDefinition = new CreationDefinition {
                        m_Prefab = MarkerPrefab,
                    };

                    ECB.AddComponent(entity, creationDefinition);
                    ECB.AddComponent<Updated>(entity);

                    var objectDefinition = new ObjectDefinition {
                        m_Position = position,
                    };

                    ECB.AddComponent(entity, objectDefinition);
                }
            }
        }
    }
}