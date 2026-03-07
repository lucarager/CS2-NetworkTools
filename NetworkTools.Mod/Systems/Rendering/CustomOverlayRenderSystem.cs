namespace NetworkTools.Systems.Rendering {
    using System;
    using System.Collections.Generic;
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Game;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.HighDefinition;

    public partial class CustomOverlayRenderSystem : GameSystemBase {
        public enum CustomMeshType {
            Cylinder,
            Arrow,
            Plane,
            NumCustomMeshTypes
        }

        [Flags]
        public enum StyleFlags {
            Grid           = 1,
            Projected      = 2,
            DepthFadeBelow = 4
        }

        private ComputeBuffer         m_AbsoluteBuffer;
        private NativeList<CurveData> m_AbsoluteData;

        private int m_AbsoluteInstanceCount;

        private Material m_AbsoluteMaterial;

        private List<uint> m_ArgsArray;

        private ComputeBuffer           m_ArgsBuffer;
        private NativeValue<BoundsData> m_BoundsData;

        private Mesh      m_BoxMesh;
        private JobHandle m_BufferWriters;

        private int m_CurveBufferID;

        private readonly ComputeBuffer[] m_CustomMeshBuffer = new ComputeBuffer[3];

        private          int                           m_CustomMeshBufferID;
        private readonly NativeList<CustomMeshdData>[] m_CustomMeshData = new NativeList<CustomMeshdData>[3];

        private Mesh[] m_customMeshes = new Mesh[3];

        private readonly int[]                       m_CustomMeshInstanceCount = new int[3];
        private          NativeList<CustomMeshdData> m_CustomMeshJobData;

        private readonly Material[] m_CustomMeshMaterial = new Material[3];

        private int m_FaceDilateID;

        private int          m_GradientScaleID;
        private PrefabSystem m_PrefabSystem;

        private ComputeBuffer         m_ProjectedBuffer;
        private NativeList<CurveData> m_ProjectedData;

        private int m_ProjectedInstanceCount;

        private Material m_ProjectedMaterial;

        private Mesh            m_QuadMesh;
        private RenderingSystem m_RenderingSystem;

        private int           m_ScaleRatioAID;
        private EntityQuery   m_SettingsQuery;
        private TerrainSystem m_TerrainSystem;

        private Material m_textMaterial;

        protected override void OnCreate() {
            base.OnCreate();
            m_RenderingSystem = World.GetOrCreateSystemManaged<RenderingSystem>();
            m_TerrainSystem   = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_PrefabSystem    = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Register with render pipeline
            RenderPipelineManager.beginContextRendering += Render;
        }

        protected override void OnDestroy() {
            // Unregister from render pipeline
            RenderPipelineManager.beginContextRendering -= Render;
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            m_BufferWriters.Complete();
            m_BufferWriters          = default;
            m_ProjectedInstanceCount = 0;
            m_AbsoluteInstanceCount  = 0;

            for (var i = 0; i < 3; i++) {
                m_CustomMeshInstanceCount[i] = 0;
            }

            if ((!m_ProjectedData.IsCreated || m_ProjectedData.Length == 0) &&
                (!m_AbsoluteData.IsCreated  || m_AbsoluteData.Length  == 0)) {
                return;
            }

            if (m_SettingsQuery.IsEmptyIgnoreFilter) {
                if (m_ProjectedData.IsCreated) {
                    m_ProjectedData.Clear();
                }

                if (m_AbsoluteData.IsCreated) {
                    m_AbsoluteData.Clear();
                }

                foreach (var nativeList in m_CustomMeshData) {
                    if (nativeList.IsCreated) {
                        nativeList.Clear();
                    }
                }

                return;
            }

            if (m_ProjectedData.IsCreated && m_ProjectedData.Length != 0) {
                m_ProjectedInstanceCount = m_ProjectedData.Length;
                GetCurveMaterial(ref m_ProjectedMaterial, true);
                GetCurveBuffer(ref m_ProjectedBuffer, m_ProjectedInstanceCount);
                m_ProjectedBuffer.SetData(m_ProjectedData.AsArray(), 0, 0, m_ProjectedInstanceCount);
                m_ProjectedMaterial.SetBuffer(m_CurveBufferID, m_ProjectedBuffer);
                m_ProjectedData.Clear();
            }

            if (m_AbsoluteData.IsCreated && m_AbsoluteData.Length != 0) {
                m_AbsoluteInstanceCount = m_AbsoluteData.Length;
                GetCurveMaterial(ref m_AbsoluteMaterial, false);
                GetCurveBuffer(ref m_AbsoluteBuffer, m_AbsoluteInstanceCount);
                m_AbsoluteBuffer.SetData(m_AbsoluteData.AsArray(), 0, 0, m_AbsoluteInstanceCount);
                m_AbsoluteMaterial.SetBuffer(m_CurveBufferID, m_AbsoluteBuffer);
                m_AbsoluteData.Clear();
            }

            foreach (var customMeshdData in m_CustomMeshJobData) {
                m_CustomMeshData[customMeshdData.m_CustomMeshType].Add(in customMeshdData);
            }

            m_CustomMeshJobData.Clear();
            for (var k = 0; k < 3; k++) {
                if (m_CustomMeshData[k].IsCreated && m_CustomMeshData[k].Length != 0) {
                    var flag = k == 2;
                    m_CustomMeshInstanceCount[k] = m_CustomMeshData[k].Length;
                    GetSolidObjectMaterial(ref m_CustomMeshMaterial[k]);
                    GetCustomMeshBuffer(ref m_CustomMeshBuffer[k], m_CustomMeshInstanceCount[k]);
                    m_CustomMeshBuffer[k].SetData(m_CustomMeshData[k].AsArray(), 0, 0, m_CustomMeshInstanceCount[k]);
                    m_CustomMeshMaterial[k].SetBuffer(m_CustomMeshBufferID, m_CustomMeshBuffer[k]);
                    m_CustomMeshMaterial[k].SetFloat("_TransparentSortPriority", k);
                    var localKeyword = new LocalKeyword(m_CustomMeshMaterial[k].shader, "DEPTH_FADE_TERRAIN_EDGE");
                    m_CustomMeshMaterial[k].SetKeyword(in localKeyword, flag);
                    HDMaterial.ValidateMaterial(m_CustomMeshMaterial[k]);
                    m_CustomMeshData[k].Clear();
                }
            }
        }


        /// <summary>
        ///     Render method called by the render pipeline.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cameras"></param>
        private void Render(ScriptableRenderContext context, List<Camera> cameras) {
            if (!m_RenderingSystem.hideOverlay) {
            }
        }

        /// <summary>
        ///     Public Buffer getter method.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <returns></returns>
        public Buffer GetBuffer(out JobHandle dependencies) {
            if (!m_ProjectedData.IsCreated) {
                m_ProjectedData = new NativeList<CurveData>(Allocator.Persistent);
            }

            if (!m_AbsoluteData.IsCreated) {
                m_AbsoluteData = new NativeList<CurveData>(Allocator.Persistent);
            }

            if (!m_CustomMeshJobData.IsCreated) {
                m_CustomMeshJobData = new NativeList<CustomMeshdData>(Allocator.Persistent);
            }

            for (var i = 0; i < 3; i++) {
                if (!m_CustomMeshData[i].IsCreated) {
                    m_CustomMeshData[i] =
                        new NativeList<CustomMeshdData>(Allocator.Persistent);
                }
            }

            if (!m_BoundsData.IsCreated) {
                m_BoundsData = new NativeValue<BoundsData>(Allocator.Persistent);
            }

            dependencies = m_BufferWriters;
            return new Buffer(m_ProjectedData,
                              m_AbsoluteData,
                              m_CustomMeshJobData,
                              m_BoundsData,
                              m_TerrainSystem.heightScaleOffset.y - 50f,
                              m_TerrainSystem.heightScaleOffset.x + 100f);
        }

        public void AddBufferWriter(JobHandle handle) {
            m_BufferWriters = JobHandle.CombineDependencies(m_BufferWriters, handle);
        }

        private void GetCustomMeshMesh(ref Mesh mesh, OverlayRenderSystem.CustomMeshType meshType) {
            if (mesh != null) {
                return;
            }

            switch (meshType) {
                case OverlayRenderSystem.CustomMeshType.Cylinder:
                {
                    var num = 64;
                    mesh = new Mesh();
                    var array  = new Vector3[num * 2];
                    var array2 = new int[num     * 6];
                    var num2   = 360f / num;
                    for (var i = 0; i < num; i++) {
                        var num3 = 0.017453292f * i * num2;
                        var num4 = Mathf.Sin(num3);
                        var num5 = Mathf.Cos(num3);
                        array[i]       = new Vector3(num4, 0.5f,  num5);
                        array[i + num] = new Vector3(num4, -0.5f, num5);
                        var num6 = i       * 6;
                        var num7 = (i + 1) % num;
                        array2[num6]     = i;
                        array2[num6 + 1] = i    + num;
                        array2[num6 + 2] = num7 + num;
                        array2[num6 + 3] = i;
                        array2[num6 + 4] = num7 + num;
                        array2[num6 + 5] = num7;
                    }

                    mesh.vertices  = array;
                    mesh.triangles = array2;
                    mesh.RecalculateNormals();
                    return;
                }
                case OverlayRenderSystem.CustomMeshType.Arrow:
                    GetArrowMesh(ref mesh);
                    return;
                case OverlayRenderSystem.CustomMeshType.Plane:
                {
                    mesh = new Mesh();
                    var array  = new Vector3[4];
                    var array2 = new int[6];
                    array[0]       = new Vector3(-0.5f, 0f, 0.5f);
                    array[1]       = new Vector3(0.5f,  0f, 0.5f);
                    array[2]       = new Vector3(0.5f,  0f, -0.5f);
                    array[3]       = new Vector3(-0.5f, 0f, -0.5f);
                    array2[0]      = 0;
                    array2[1]      = 1;
                    array2[2]      = 2;
                    array2[3]      = 2;
                    array2[4]      = 3;
                    array2[5]      = 0;
                    mesh.vertices  = array;
                    mesh.triangles = array2;
                    mesh.RecalculateNormals();
                    return;
                }
                default:
                    return;
            }
        }

        private void GetArrowMesh(ref Mesh mesh) {
            if (mesh != null) {
                return;
            }

            mesh = new Mesh();
            var num  = 0.5f;
            var num2 = 1.5f;
            var num3 = 2f;
            var num4 = 1f;
            var array = new[] {
                new Vector3(-num  * 0.5f, 0f,          0f),
                new Vector3(num   * 0.5f, 0f,          0f),
                new Vector3(num   * 0.5f, num3,        0f),
                new Vector3(-num  * 0.5f, num3,        0f),
                new Vector3(num2  * 0.5f, num3,        0f),
                new Vector3(-num2 * 0.5f, num3,        0f),
                new Vector3(0f,           num3 + num4, 0f)
            };
            var array2 = new[] {
                0, 3, 1, 1, 3, 2, 3, 5, 6, 2,
                3, 6, 4, 2, 6
            };
            mesh.vertices  = array;
            mesh.triangles = array2;
            mesh.RecalculateNormals();
        }

        private void GetMesh(ref Mesh mesh, bool box, bool cameraFacing = true) {
            if (mesh == null) {
                mesh      = new Mesh();
                mesh.name = "Overlay";
                if (box) {
                    mesh.vertices = new[] {
                        new Vector3(-1f, 0f, -1f),
                        new Vector3(-1f, 0f, 1f),
                        new Vector3(1f,  0f, 1f),
                        new Vector3(1f,  0f, -1f),
                        new Vector3(-1f, 1f, -1f),
                        new Vector3(-1f, 1f, 1f),
                        new Vector3(1f,  1f, 1f),
                        new Vector3(1f,  1f, -1f)
                    };
                    mesh.triangles = new[] {
                        0, 1, 5, 5, 4, 0, 3, 7, 6, 6,
                        2, 3, 0, 3, 2, 2, 1, 0, 4, 5,
                        6, 6, 7, 4, 0, 4, 7, 7, 3, 0,
                        1, 2, 6, 6, 5, 1
                    };
                    return;
                }

                mesh.vertices = new[] {
                    new Vector3(-1f, 0f, -1f),
                    new Vector3(-1f, 0f, 1f),
                    new Vector3(1f,  0f, 1f),
                    new Vector3(1f,  0f, -1f)
                };
                mesh.triangles = new[] {
                    0, 3, 2, 2, 1, 0, 0, 1, 2, 2,
                    3, 0
                };
            }
        }

        private void GetCurveMaterial(ref Material material, bool projected) {
            if (material == null) {
                var singletonPrefab = m_PrefabSystem.GetSingletonPrefab<OverlayConfigurationPrefab>(m_SettingsQuery);
                material      = new Material(singletonPrefab.m_CurveMaterial);
                material.name = "Overlay curves";
                if (projected) {
                    material.EnableKeyword("PROJECTED_MODE");
                }
            }
        }

        private void GetSolidObjectMaterial(ref Material material) {
            if (material == null) {
                var singletonPrefab = m_PrefabSystem.GetSingletonPrefab<OverlayConfigurationPrefab>(m_SettingsQuery);
                material      = new Material(singletonPrefab.m_SolidObjectMaterial);
                material.name = "Overlay Object";
            }
        }

        private unsafe void GetCurveBuffer(ref ComputeBuffer buffer, int count) {
            if (buffer != null && buffer.count < count) {
                count = math.max(buffer.count * 2, count);
                buffer.Release();
                buffer = null;
            }

            if (buffer == null) {
                buffer      = new ComputeBuffer(math.max(64, count), sizeof(OverlayRenderSystem.CurveData));
                buffer.name = "Overlay curve buffer";
            }
        }

        private unsafe void GetCustomMeshBuffer(ref ComputeBuffer buffer, int count) {
            if (buffer != null && buffer.count < count) {
                count = math.max(buffer.count * 2, count);
                buffer.Release();
                buffer = null;
            }

            if (buffer == null) {
                buffer      = new ComputeBuffer(math.max(64, count), sizeof(OverlayRenderSystem.CustomMeshdData));
                buffer.name = "Overlay cylinder buffer";
            }
        }


        public struct CurveData {
            public Matrix4x4 m_Matrix;

            public Matrix4x4 m_InverseMatrix;

            public Matrix4x4 m_Curve;

            public Color m_OutlineColor;

            public Color m_FillColor;

            public float2 m_Size;

            public float2 m_DashLengths;

            public float2 m_Roundness;

            public float m_OutlineWidth;

            public float m_FillStyle;

            public float m_DepthFadeStyle;
        }

        public struct CustomMeshdData {
            public Matrix4x4 m_Matrix;

            public Matrix4x4 m_InverseMatrix;

            public Color m_FillColor;

            public float2 m_Size;

            public int m_CustomMeshType;
        }

        public struct BoundsData {
            public Bounds3 m_CurveBounds;
        }

        /// <summary>
        ///     Overlay render buffer
        /// </summary>
        public struct Buffer {
            public Buffer(NativeList<CurveData>       projectedCurves,
                          NativeList<CurveData>       absoluteCurves,
                          NativeList<CustomMeshdData> custMeshesData,
                          NativeValue<BoundsData>     bounds,
                          float                       positionY,
                          float                       scaleY) {
                m_ProjectedCurves = projectedCurves;
                m_AbsoluteCurves  = absoluteCurves;
                m_CustomMeshes    = custMeshesData;
                m_Bounds          = bounds;
                m_PositionY       = positionY;
                m_ScaleY          = scaleY;
            }

            public void DrawCircle(Color color, float3 position, float diameter) {
                DrawCircleImpl(color, color, 0f, 0, new float2(0f, 1f), position, diameter);
            }

            public void DrawCircle(Color      outlineColor, Color  fillColor, float  outlineWidth,
                                   StyleFlags styleFlags,   float2 direction, float3 position,
                                   float      diameter) {
                DrawCircleImpl(outlineColor, fillColor, outlineWidth, styleFlags, direction, position, diameter);
            }

            public void DrawCustomMesh(Color          fillColor, float3 position, float height, float width,
                                       CustomMeshType meshType) {
                DrawCustomMesh(fillColor, position, height, width, meshType, Quaternion.identity);
            }

            public void DrawCustomMesh(Color          fillColor, float3     position, float height, float width,
                                       CustomMeshType meshType,  Quaternion rot) {
                CustomMeshdData customMeshdData;
                customMeshdData.m_Size           = new float2(height, width);
                customMeshdData.m_FillColor      = fillColor.linear;
                customMeshdData.m_Matrix         = Matrix4x4.TRS(position, rot, new float3(width, height, width));
                customMeshdData.m_InverseMatrix  = customMeshdData.m_Matrix.inverse;
                customMeshdData.m_CustomMeshType = (int)meshType;
                m_CustomMeshes.Add(in customMeshdData);
            }

            private void DrawCircleImpl(Color      outlineColor, Color  fillColor, float  outlineWidth,
                                        StyleFlags styleFlags,   float2 direction, float3 position,
                                        float      diameter) {
                CurveData curveData;
                curveData.m_Size           = new float2(diameter, diameter);
                curveData.m_DashLengths    = new float2(0f,       diameter);
                curveData.m_Roundness      = new float2(1f,       1f);
                curveData.m_OutlineWidth   = outlineWidth;
                curveData.m_FillStyle      = (float)(styleFlags & StyleFlags.Grid);
                curveData.m_DepthFadeStyle = (float)(styleFlags & StyleFlags.DepthFadeBelow);
                curveData.m_Curve = new Matrix4x4(new float4(position, 0f),
                                                  new float4(position, 0f),
                                                  new float4(position, 0f),
                                                  new float4(position, 0f));
                curveData.m_OutlineColor = outlineColor.linear;
                curveData.m_FillColor    = fillColor.linear;
                Bounds3 bounds;
                if ((styleFlags & StyleFlags.Projected) != 0) {
                    curveData.m_Matrix        = FitBox(direction, position, diameter, out bounds);
                    curveData.m_InverseMatrix = curveData.m_Matrix.inverse;
                    m_ProjectedCurves.Add(in curveData);
                } else {
                    curveData.m_Matrix        = FitQuad(direction, position, diameter, out bounds);
                    curveData.m_InverseMatrix = curveData.m_Matrix.inverse;
                    m_AbsoluteCurves.Add(in curveData);
                }

                var value = m_Bounds.value;
                value.m_CurveBounds |= bounds;
                m_Bounds.value      =  value;
            }

            public void DrawLine(Color color, Line3.Segment line, float width, bool cameraFacing = false) {
                var num = MathUtils.Length(line.xz);
                DrawCurveImpl(color,
                              color,
                              0f,
                              0,
                              NetUtils.StraightCurve(line.a, line.b),
                              width,
                              num + width * 2f,
                              0f,
                              default,
                              num,
                              cameraFacing);
            }

            public void DrawLine(Color      outlineColor, Color         fillColor, float outlineWidth,
                                 StyleFlags styleFlags,   Line3.Segment line,      float width,
                                 float2     roundness) {
                var num = MathUtils.Length(line.xz);
                DrawCurveImpl(outlineColor,
                              fillColor,
                              outlineWidth,
                              styleFlags,
                              NetUtils.StraightCurve(line.a, line.b),
                              width,
                              num + width * 2f,
                              0f,
                              roundness,
                              num);
            }

            public void DrawDashedLine(Color color, Line3.Segment line, float width, float dashLength,
                                       float gapLength) {
                DrawCurveImpl(color,
                              color,
                              0f,
                              0,
                              NetUtils.StraightCurve(line.a, line.b),
                              width,
                              dashLength,
                              gapLength,
                              default,
                              MathUtils.Length(line.xz));
            }

            public void DrawDashedLine(Color      outlineColor, Color         fillColor, float outlineWidth,
                                       StyleFlags styleFlags,   Line3.Segment line,      float width,
                                       float      dashLength,   float         gapLength) {
                DrawCurveImpl(outlineColor,
                              fillColor,
                              outlineWidth,
                              styleFlags,
                              NetUtils.StraightCurve(line.a, line.b),
                              width,
                              dashLength,
                              gapLength,
                              default,
                              MathUtils.Length(line.xz));
            }

            public void DrawDashedLine(Color  color, Line3.Segment line, float width, float dashLength, float gapLength,
                                       float2 roundness) {
                DrawCurveImpl(color,
                              color,
                              0f,
                              0,
                              NetUtils.StraightCurve(line.a, line.b),
                              width,
                              dashLength,
                              gapLength,
                              roundness,
                              MathUtils.Length(line.xz));
            }

            public void DrawDashedLine(Color      outlineColor, Color         fillColor, float  outlineWidth,
                                       StyleFlags styleFlags,   Line3.Segment line,      float  width,
                                       float      dashLength,   float         gapLength, float2 roundness) {
                DrawCurveImpl(outlineColor,
                              fillColor,
                              outlineWidth,
                              styleFlags,
                              NetUtils.StraightCurve(line.a, line.b),
                              width,
                              dashLength,
                              gapLength,
                              roundness,
                              MathUtils.Length(line.xz));
            }

            public void DrawCurve(Color color, Bezier4x3 curve, float width) {
                var num = MathUtils.Length(curve.xz);
                DrawCurveImpl(color, color, 0f, 0, curve, width, num + width * 2f, 0f, default, num);
            }

            public void DrawCurve(Color      outlineColor, Color     fillColor, float outlineWidth,
                                  StyleFlags styleFlags,   Bezier4x3 curve,     float width) {
                var num = MathUtils.Length(curve.xz);
                DrawCurveImpl(outlineColor,
                              fillColor,
                              outlineWidth,
                              styleFlags,
                              curve,
                              width,
                              num + width * 2f,
                              0f,
                              default,
                              num);
            }

            public void DrawCurve(Color color, Bezier4x3 curve, float width, float2 roundness) {
                var num = MathUtils.Length(curve.xz);
                DrawCurveImpl(color, color, 0f, 0, curve, width, num + width * 2f, 0f, roundness, num);
            }

            public void DrawCurve(Color      outlineColor, Color     fillColor, float outlineWidth,
                                  StyleFlags styleFlags,   Bezier4x3 curve,     float width,
                                  float2     roundness) {
                var num = MathUtils.Length(curve.xz);
                DrawCurveImpl(outlineColor,
                              fillColor,
                              outlineWidth,
                              styleFlags,
                              curve,
                              width,
                              num + width * 2f,
                              0f,
                              roundness,
                              num);
            }

            public void DrawDashedCurve(Color color, Bezier4x3 curve, float width, float dashLength, float gapLength) {
                DrawCurveImpl(color,
                              color,
                              0f,
                              0,
                              curve,
                              width,
                              dashLength,
                              gapLength,
                              default,
                              MathUtils.Length(curve.xz));
            }

            public void DrawDashedCurve(Color      outlineColor, Color     fillColor, float outlineWidth,
                                        StyleFlags styleFlags,   Bezier4x3 curve,     float width,
                                        float      dashLength,   float     gapLength) {
                DrawCurveImpl(outlineColor,
                              fillColor,
                              outlineWidth,
                              styleFlags,
                              curve,
                              width,
                              dashLength,
                              gapLength,
                              default,
                              MathUtils.Length(curve.xz));
            }

            private void DrawCurveImpl(Color      outlineColor, Color     fillColor, float  outlineWidth,
                                       StyleFlags styleFlags,   Bezier4x3 curve,     float  width,
                                       float      dashLength,   float     gapLength, float2 roundness, float length,
                                       bool       cameraFacing = false) {
                if (length < 0.01f) {
                    return;
                }

                CurveData curveData;
                curveData.m_Size           = new float2(width,     length);
                curveData.m_DashLengths    = new float2(gapLength, dashLength);
                curveData.m_Roundness      = roundness;
                curveData.m_OutlineWidth   = outlineWidth;
                curveData.m_FillStyle      = (float)(styleFlags & StyleFlags.Grid);
                curveData.m_DepthFadeStyle = (float)(styleFlags & StyleFlags.DepthFadeBelow);
                curveData.m_Curve          = BuildCurveMatrix(curve, length);
                curveData.m_OutlineColor   = outlineColor.linear;
                curveData.m_FillColor      = fillColor.linear;
                Bounds3 bounds;
                if ((styleFlags & StyleFlags.Projected) != 0) {
                    curveData.m_Matrix        = FitBox(curve, width, out bounds);
                    curveData.m_InverseMatrix = curveData.m_Matrix.inverse;
                    m_ProjectedCurves.Add(in curveData);
                } else {
                    curveData.m_Matrix        = FitQuad(curve, width, out bounds, cameraFacing);
                    curveData.m_InverseMatrix = curveData.m_Matrix.inverse;
                    m_AbsoluteCurves.Add(in curveData);
                }

                var value = m_Bounds.value;
                value.m_CurveBounds |= bounds;
                m_Bounds.value      =  value;
            }

            private Matrix4x4 FitBox(float2 direction, float3 position, float extend, out Bounds3 bounds) {
                bounds        = new Bounds3(position, position);
                bounds.min.xz = bounds.min.xz - extend;
                bounds.max.xz = bounds.max.xz + extend;
                bounds.min.y  = m_PositionY;
                bounds.max.y  = m_ScaleY;
                position.y    = m_PositionY;
                var quat   = quaternion.RotateY(math.atan2(direction.x, direction.y));
                var @float = new float3(extend, m_ScaleY, extend);
                return Matrix4x4.TRS(position, quat, @float);
            }

            private Matrix4x4 FitQuad(float2 direction, float3 position, float extend, out Bounds3 bounds) {
                bounds        = new Bounds3(position, position);
                bounds.min.xz = bounds.min.xz - extend;
                bounds.max.xz = bounds.max.xz + extend;
                var quat   = quaternion.RotateY(math.atan2(direction.x, direction.y));
                var @float = new float3(extend, 1f, extend);
                return Matrix4x4.TRS(position, quat, @float);
            }

            private Matrix4x4 FitBox(Bezier4x3 curve, float extend, out Bounds3 bounds) {
                bounds        = MathUtils.Bounds(curve);
                bounds.min.xz = bounds.min.xz - extend;
                bounds.max.xz = bounds.max.xz + extend;
                bounds.min.y  = m_PositionY;
                bounds.max.y  = m_ScaleY;
                var @float = new float3(0f, m_PositionY, 0f);
                var quat   = quaternion.identity;
                var float2 = new float3(0f, m_ScaleY, 0f);
                var float3 = curve.d.xz - curve.a.xz;
                if (MathUtils.TryNormalize(ref float3)) {
                    var float4  = MathUtils.Right(float3);
                    var float5  = curve.b.xz - curve.a.xz;
                    var float6  = curve.c.xz - curve.a.xz;
                    var float7  = curve.d.xz - curve.a.xz;
                    var float8  = new float2(math.dot(float5, float4), math.dot(float5, float3));
                    var float9  = new float2(math.dot(float6, float4), math.dot(float6, float3));
                    var float10 = new float2(math.dot(float7, float4), math.dot(float7, float3));
                    var float11 = math.min(math.min(0f, float8), math.min(float9, float10));
                    var float12 = math.max(math.max(0f, float8), math.max(float9, float10));
                    var float13 = math.lerp(float11, float12, 0.5f);
                    quat      = quaternion.LookRotation(new float3(float3.x, 0f, float3.y), new float3(0f, 1f, 0f));
                    @float.xz = curve.a.xz                 + math.rotate(quat, new float3(float13.x, 0f, float13.y)).xz;
                    float2.xz = (float12 - float11) * 0.5f + extend;
                } else {
                    @float.xz = MathUtils.Center(bounds.xz);
                    quat      = quaternion.identity;
                    float2.xz = MathUtils.Extents(bounds.xz);
                }

                return Matrix4x4.TRS(@float, quat, float2);
            }

            private Matrix4x4 FitQuad(Bezier4x3 curve, float extend, out Bounds3 bounds, bool cameraFacing = false) {
                bounds        = MathUtils.Bounds(curve);
                bounds.min.xz = bounds.min.xz - extend;
                bounds.max.xz = bounds.max.xz + extend;
                var    @float = MathUtils.Center(bounds);
                var    quat   = quaternion.identity;
                float3 float2 = 0f;
                float2.xz = MathUtils.Extents(bounds.xz);
                float2.y  = 1f;
                var float3 = curve.d - curve.a;
                var num    = math.length(float3);
                if (num > 0.1f) {
                    float3 /= num;
                    var float4 = math.cross(float3, curve.b - curve.a);
                    var float5 = math.cross(float3, curve.d - curve.c);
                    float4 = math.select(float4, -float4, float4.y < 0f);
                    float5 = math.select(float5, -float5, float5.y < 0f);
                    var float6 = float4 + float5;
                    var num2   = math.length(float6);
                    float6 = math.lerp(new float3(0f, 1f, 0f), float6, math.saturate(num2 / num * 10f));
                    float6 = math.normalize(float6);
                    var float7 = math.cross(float6, float3);
                    if (MathUtils.TryNormalize(ref float7)) {
                        var float8 = curve.b - curve.a;
                        var float9 = curve.c - curve.a;
                        var float10 = curve.d - curve.a;
                        var float11 = new float2(math.dot(float8, float7), math.dot(float8, float3));
                        var float12 = new float2(math.dot(float9, float7), math.dot(float9, float3));
                        var float13 = new float2(math.dot(float10, float7), math.dot(float10, float3));
                        var float14 = math.min(math.min(0f, float11), math.min(float12, float13));
                        var float15 = math.max(math.max(0f, float11), math.max(float12, float13));
                        var float16 = math.lerp(float14, float15, 0.5f);
                        quat      = quaternion.LookRotation(float3, float6);
                        @float    = curve.a + math.rotate(quat, new float3(float16.x, 0f, float16.y));
                        float2.xz = (float15 - float14) * 0.5f + extend;
                    }
                }

                return Matrix4x4.TRS(@float, quat, float2);
            }

            private static float4x4 BuildCurveMatrix(Bezier4x3 curve, float length) {
                float2 @float;
                @float.x =  math.distance(curve.a, curve.b);
                @float.y =  math.distance(curve.c, curve.d);
                @float   /= length;
                return new float4x4 {
                    c0 = new float4(curve.a, 0f),
                    c1 = new float4(curve.b, @float.x),
                    c2 = new float4(curve.c, 1f - @float.y),
                    c3 = new float4(curve.d, 1f)
                };
            }

            private NativeList<CurveData> m_ProjectedCurves;

            private NativeList<CurveData> m_AbsoluteCurves;

            private NativeList<CustomMeshdData> m_CustomMeshes;

            private NativeValue<BoundsData> m_Bounds;

            private readonly float m_PositionY;

            private readonly float m_ScaleY;
        }
    }
}