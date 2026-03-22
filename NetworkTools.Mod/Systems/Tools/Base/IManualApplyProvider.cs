namespace NetworkTools.Systems.Tools {
    using Unity.Entities;

    /// <summary>
    ///     Implemented by tool systems that require a manual apply trigger.
    /// </summary>
    public interface IManualApplyProvider {
        void RequestApply();
    }
}
