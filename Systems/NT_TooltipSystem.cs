using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.Prefabs;
using NetworkTools.Extensions;
using UnityEngine.Diagnostics;

// <copyright file="P_UISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    using Colossal.UI.Binding;
    using Game.UI.Tooltip;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_TooltipSystem : TooltipSystemBase {
        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
        }
    }
}