namespace NetworkTools.Systems.Tools {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Base;

    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Handle management for <see cref="NT_GenerateToolSystem"/>.
    /// </summary>
    public partial class NT_GenerateToolSystem {
        /// <summary>
        ///     Creates or refreshes handles based on the current config.
        /// </summary>
        private void RefreshTransformHandles() {
            DestroyAllHandles();

            m_Log.Debug("RefreshTransformHandles: Creating handles");

            var handleDefs = GetHandleDefinitions();
            CreateHandlesFromDefinitions(handleDefs);
        }

        /// <summary>
        ///     Builds handle definitions for the current grid config.
        ///     Creates parameter handles for Angle, XSpacing, and ZSpacing.
        /// </summary>
        private TransformHandleDefinition[] GetHandleDefinitions() {
            var startPos = CurrentConfig.StartPosition;
            var endPos   = CurrentConfig.EndPosition;
            var midPoint = (startPos + endPos) * 0.5f;

            // Direction vector for placing spacing handles along the grid axes
            var angleRad  = math.radians(CurrentConfig.Angle);
            var xDir      = new float3(math.cos(angleRad), 0f, math.sin(angleRad));
            var yDir      = new float3(-math.sin(angleRad), 0f, math.cos(angleRad));

            return new[] {
                // Angle handle at start point
                new TransformHandleDefinition {
                    Key       = HandleKeys.Angle,
                    Position  = startPos,
                    TypeFlags =  HandleTypeFlags.Parameter | HandleTypeFlags.Rotation | HandleTypeFlags.Primary,
                    Value = 0,
                    Angle = 0,
                },
                // Start position handle
                new TransformHandleDefinition {
                    Key       = HandleKeys.StartPosition,
                    Position  = startPos,
                    TypeFlags = HandleTypeFlags.Parameter | HandleTypeFlags.Position | HandleTypeFlags.Primary,
                    Radius    = NT_Handle.PrimaryRadius
                },
                // X Spacing handle offset along X axis
                new TransformHandleDefinition {
                    Key       = HandleKeys.XSpacing,
                    Position  = startPos + xDir * CurrentConfig.XSpacing,
                    TypeFlags = HandleTypeFlags.Parameter | HandleTypeFlags.Secondary | HandleTypeFlags.ParameterRange,
                    Value     = CurrentConfig.XSpacing,
                    MinValue  = GenerateConfig.MinSpacing,
                    MaxValue  = GenerateConfig.MaxSpacing
                },
                // Y Spacing handle offset along Y axis
                new TransformHandleDefinition {
                    Key       = HandleKeys.YSpacing,
                    Position  = startPos + yDir * CurrentConfig.ZSpacing,
                    TypeFlags = HandleTypeFlags.Parameter | HandleTypeFlags.Secondary | HandleTypeFlags.ParameterRange,
                    Value     = CurrentConfig.ZSpacing,
                    MinValue  = GenerateConfig.MinSpacing,
                    MaxValue  = GenerateConfig.MaxSpacing
                }
            };
        }

        /// <inheritdoc />
        protected override void OnParameterHandleDragged(Entity handle, int key, float3 position, float value) {
            m_Log.Debug($"OnParameterHandleDragged: key={key}, value={value}");
            ApplyConfigValue(key, value);
        }

        /// <inheritdoc />
        protected override void OnPositionHandleDragged(Entity handle, int key, float3 position) {
            m_Log.Debug($"OnPositionHandleDragged: key={key}, position={position}");
            ApplyConfigPosition(key, position);
        }

        /// <summary>
        ///     Gets the current config value for a parameter handle key.
        /// </summary>
        private float GetConfigValue(int key) {
            return key switch {
                HandleKeys.Angle    => CurrentConfig.Angle,
                HandleKeys.XSpacing => CurrentConfig.XSpacing,
                HandleKeys.YSpacing => CurrentConfig.ZSpacing,
                _                   => 0f
            };
        }

        /// <summary>
        ///     Writes a scalar value to the config field identified by the handle key.
        /// </summary>
        private void ApplyConfigValue(int key, float value) {
            switch (key) {
                case HandleKeys.Angle:
                    CurrentConfig.Angle = value;
                    break;
                case HandleKeys.XSpacing:
                    CurrentConfig.XSpacing = value;
                    break;
                case HandleKeys.YSpacing:
                    CurrentConfig.ZSpacing = value;
                    break;
            }
        }

        /// <summary>
        ///     Writes a position to the config field identified by the handle key.
        /// </summary>
        private void ApplyConfigPosition(int key, float3 position) {
            switch (key) {
                case HandleKeys.StartPosition:
                    CurrentConfig.StartPosition = position;
                    break;
                case HandleKeys.EndPosition:
                    CurrentConfig.EndPosition = position;
                    break;
            }
        }
    }
}
