namespace NetworkTools.Systems.Tools.Generate {
    /// <summary>
    ///     Constants for transform handle identification in the Grid tool.
    ///     Used in NT_HandleLink.Key to map handles to config parameters.
    /// </summary>
    public static class HandleKeys {
        // Grid position handles (200-209)
        public const int StartPosition = 200;
        public const int EndPosition   = 201;

        // Grid parameter handles (210-219)
        public const int Angle    = 210;
        public const int XSpacing = 211;
        public const int YSpacing = 212;
    }
}
