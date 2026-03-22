// <copyright file="HintTooltipEntry.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Game.Input;

    /// <summary>
    ///     Configuration for a single hint tooltip entry.
    ///     Wraps text in Common.ACTION[] format when no action is provided.
    /// </summary>
    public record HintTooltipEntry {
        /// <summary>
        ///     The localization key or formatted text for the tooltip.
        /// </summary>
        public string Text { get; }

        /// <summary>
        ///     The input action to display alongside the tooltip, or null for text-only.
        /// </summary>
        public ProxyAction Action { get; }

        public HintTooltipEntry(string text, ProxyAction action = null) {
            Action = action;
            Text = action == null ? $"Common.ACTION[{text}]" : text;
        }
    }
}
