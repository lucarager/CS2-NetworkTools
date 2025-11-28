// <copyright file="NT_BaseToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Jobs;
    using Utils;

    #endregion

    public abstract partial class NT_BaseToolSystem : ToolBaseSystem {

        private         PrefabBase     m_Prefab;
        internal        PrefixedLogger m_Log;
        public override string         toolID => "NT_BaseToolSystem";
        public          bool           ShowNodes = false;

        protected override void OnCreate() {
            Enabled = false;
            m_Log   = new PrefixedLogger(nameof(NT_BaseToolSystem));
            m_Log.Debug("OnCreate()");

            base.OnCreate();
        }

        protected override void OnDestroy() { base.OnDestroy(); }

        public override PrefabBase GetPrefab() { return m_Prefab; }

        public void RequestEnable() { m_ToolSystem.activeTool = this; }

        public void RequestDisable() { m_ToolSystem.activeTool = m_DefaultToolSystem; }

        protected override JobHandle OnUpdate(JobHandle inputDeps) { return inputDeps; }
    }
}