namespace NetworkTools.Systems.Tools {
    /// <summary>
    ///     Implemented by tool systems that expose a manual "Apply" button in the UI.
    ///     The presence of this interface is what makes the button visible at all; the
    ///     members below let the UI system compute the button's <see cref="ApplyState" />.
    /// </summary>
    public interface IManualApplyProvider {
        /// <summary>
        ///     Minimum number of selected nodes required before the apply button is offered.
        ///     When fewer are selected, the UI shows a hint instead of the button.
        /// </summary>
        int ApplyMinNodeCount { get; }

        /// <summary>
        ///     Tool-specific readiness: whether the current configuration is complete enough
        ///     to apply (e.g. the operation phase is ready and a mode/template is chosen).
        ///     This is combined with <see cref="NT_BaseToolSystem.GetAllowApply" /> to decide
        ///     whether the button is enabled or merely disabled.
        /// </summary>
        bool CanApply { get; }

        /// <summary>
        ///     Triggers the apply operation.
        /// </summary>
        void RequestApply();
    }
}
