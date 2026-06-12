namespace NetworkTools.Systems.Tools.Utils {
    using Colossal.Mathematics;
    using Game.Tools;
    using Unity.Entities;

    /// <summary>
    ///     Lightweight, tool-agnostic description of a single network edge.
    ///     Used by Generate, Connect, and Parallel tools to carry edge geometry
    ///     and identity through their processing pipelines before final output.
    /// </summary>
    public struct EdgeConfig {
        // === Identity ===

        /// <summary>Source edge entity, or <see cref="Entity.Null"/> for newly generated edges.</summary>
        public Entity EdgeEntity;

        /// <summary>Start node entity in path-ordered direction.</summary>
        public Entity StartNodeEntity;

        /// <summary>End node entity in path-ordered direction.</summary>
        public Entity EndNodeEntity;

        /// <summary>True if the edge's native direction matches the path direction.</summary>
        public bool IsForward;

        /// <summary>False when the edge could not be resolved (e.g. missing curve data).</summary>
        public bool IsValid;

        // === Geometry ===

        /// <summary>World-space position of the start node (may differ from <see cref="Bezier"/>.a when the bezier is shortened).</summary>
        public Unity.Mathematics.float3 StartNodePosition;

        /// <summary>World-space position of the end node (may differ from <see cref="Bezier"/>.d when the bezier is shortened).</summary>
        public Unity.Mathematics.float3 EndNodePosition;

        /// <summary>Path-ordered bezier curve of the edge.</summary>
        public Bezier4x3 Bezier;

        /// <summary>Arc length of <see cref="Bezier"/>.</summary>
        public float Length;

        /// <summary>Node rotation applied to the start <c>CoursePos</c> during output.</summary>
        public Unity.Mathematics.quaternion StartNodeRotation;

        /// <summary>Node rotation applied to the end <c>CoursePos</c> during output.</summary>
        public Unity.Mathematics.quaternion EndNodeRotation;

        /// <summary>Course-level elevation written to <c>NetCourse.m_Elevation</c> (defaults to zero).</summary>
        public Unity.Mathematics.float2 CourseElevation;

        public float StartNodeElevation;
        public float EndNodeElevation;

        // === Prefab override ===

        /// <summary>Per-edge prefab override. <see cref="Entity.Null"/> falls back to the tool's default prefab.</summary>
        public Entity NetPrefabEntity;

        /// <summary>Per-edge lane prefab override. <see cref="Entity.Null"/> falls back to the tool's default.</summary>
        public Entity NetLanePrefabEntity;

        // === Output hints ===

        /// <summary>Random seed written to the edge's <c>CreationDefinition</c> (defaults to zero).</summary>
        public int RandomSeed;

        /// <summary>Course position flags applied to the start node during output.</summary>
        public CoursePosFlags StartNodeFlags;

        /// <summary>Course position flags applied to the end node during output.</summary>
        public CoursePosFlags EndNodeFlags;
    }
}
