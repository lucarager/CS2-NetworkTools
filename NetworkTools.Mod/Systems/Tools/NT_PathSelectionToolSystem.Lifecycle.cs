// <copyright file="NT_PathSelectionToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    ///     Partial class containing lifecycle methods for selection state management.
    /// </summary>
    public abstract partial class NT_PathSelectionToolSystem {
        /// <summary>
        ///     Initializes the selection state NativeLists.
        ///     Call from derived class OnCreate after base.OnCreate.
        /// </summary>
        protected void InitializeSelectionState() {
            m_SelectedNodes = new NativeList<Entity>(32, Allocator.Persistent);
            m_EligibleNodes = new NativeList<Entity>(64, Allocator.Persistent);
            m_CurrentPathNodes = new NativeList<Entity>(32, Allocator.Persistent);
            m_CurrentPathEdges = new NativeList<Entity>(32, Allocator.Persistent);
            m_NextPathNodes = new NativeList<Entity>(32, Allocator.Persistent);
            m_NextPathEdges = new NativeList<Entity>(32, Allocator.Persistent);
        }

        /// <summary>
        ///     Disposes the selection state NativeLists.
        ///     Call from derived class OnDestroy before base.OnDestroy.
        /// </summary>
        protected void DisposeSelectionState() {
            if (m_SelectedNodes.IsCreated) m_SelectedNodes.Dispose();
            if (m_EligibleNodes.IsCreated) m_EligibleNodes.Dispose();
            if (m_CurrentPathNodes.IsCreated) m_CurrentPathNodes.Dispose();
            if (m_CurrentPathEdges.IsCreated) m_CurrentPathEdges.Dispose();
            if (m_NextPathNodes.IsCreated) m_NextPathNodes.Dispose();
            if (m_NextPathEdges.IsCreated) m_NextPathEdges.Dispose();
        }
    }
}
