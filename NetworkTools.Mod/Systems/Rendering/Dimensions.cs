namespace NetworkTools.Systems.Rendering {
    public static class NT_Dimensions {
        // -- Common
        public static readonly float LINE_WIDTH = 0.8f;

        // -- Nodes
        public static readonly float NODE_BORDER_WIDTH_REST = 0f;
        public static readonly float NODE_BORDER_WIDTH_HOVER = LINE_WIDTH;
        public static readonly float NODE_BORDER_WIDTH_ACTIVE = LINE_WIDTH;


        // -- Handles
        // ---- Point Handle
        public static readonly float HANDLE_POINT_OUTLINE_WIDTH = LINE_WIDTH;

        // ---- Circle Handle
        public static readonly float HANDLE_CIRCLE_OUTLINE_WIDTH = LINE_WIDTH;

        // ---- Rotation Handle
        public static readonly float HANDLE_ROTATION_OUTLINE_WIDTH  = LINE_WIDTH;
        public static readonly float HANDLE_ROTATION_ARROW_LENGTH   = 4f;
        public static readonly float HANDLE_ROTATION_ARROW_HEAD     = 1.5f;
        public static readonly float HANDLE_ROTATION_ARROW_WIDTH    = 0.6f;

        // ---- Axis Handle
        public static readonly float HANDLE_AXIS_ORIGIN_RADIUS    = 1f;
        public static readonly float HANDLE_AXIS_ORIGIN_OUTLINE   = 0f;
        public static readonly float HANDLE_AXIS_LINE_WIDTH       = LINE_WIDTH;
        public static readonly float HANDLE_AXIS_LINE_DASH_LENGTH = 2f;
        public static readonly float HANDLE_AXIS_LINE_DASH_GAP    = 2f;
        public static readonly float HANDLE_AXIS_CIRCLE_RADIUS    = 3f;
        public static readonly float HANDLE_AXIS_CIRCLE_OUTLINE   = LINE_WIDTH;
    }
}