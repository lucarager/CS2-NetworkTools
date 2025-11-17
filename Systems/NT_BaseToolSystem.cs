// <copyright file="NT_BaseTool.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>



namespace NetworkTools.Systems {
    using Game.Tools;
    using Unity.Jobs;
    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Unity.Entities;
    using Utils;

    public abstract partial class NT_BaseToolSystem : ToolBaseSystem {
        public override string         toolID => "ParcelTool";
        private         IProxyAction   m_ApplyAction;
        private         IProxyAction   m_SecondaryApplyAction;
        private         PrefabBase     m_Prefab;
        internal        PrefixedLogger m_Log;

        protected override void OnCreate() {
            Enabled                = false;
            m_Log                  = new PrefixedLogger(nameof(NT_BaseToolSystem));
            m_Log.Debug("OnCreate()");

            base.OnCreate();
        }

        protected override void OnDestroy() { base.OnDestroy(); }

        public override PrefabBase GetPrefab() {
            return this.m_Prefab;
        }

        public void RequestEnable() {
            m_ToolSystem.activeTool = this;
        }

        public void RequestDisable() {
            m_ToolSystem.activeTool = m_DefaultToolSystem;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) { return inputDeps; }
    }
}