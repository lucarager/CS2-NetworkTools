import { ModuleRegistry } from "cs2/modding";
import { initialize as initializeBase, ModulePath, ThemePath } from "vanilla/Components";

// Re-export the shared vanilla singletons so existing NetworkTools imports
// (`import { VC, VT, VF } from "components/vanilla/Components"`) keep working unchanged.
export { VC, VT, VF } from "vanilla/Components";

// NetworkTools-specific vanilla modules beyond the shared base set
// (see Common/ui/vanilla/Components.tsx for the base).
const extraModulePaths: ModulePath[] = [
    {
        path: "game-ui/game/data-binding/camera-bindings.ts",
        components: ["focusEntity"],
    },
    {
        path: "game-ui/menu/widgets/field/field.tsx",
        components: ["ValueField", "LocalizedValueField"],
    },
    {
        path: "game-ui/editor/widgets/fields/toggle-field.tsx",
        components: ["ToggleField"],
    },
    {
        path: "game-ui/editor/widgets/fields/int-input-field.tsx",
        components: [
            "IntInputField",
            "UIntInputField",
            "Int2InputField",
            "Int3InputField",
            "Int4InputField",
        ],
    },
    {
        path: "game-ui/editor/widgets/fields/number-slider-field.tsx",
        components: ["NumberSliderField", "IntSliderField", "UIntSliderField", "FloatSliderField"],
    },
    {
        path: "game-ui/editor/widgets/fields/float-input-field.tsx",
        components: ["FloatInputField", "Float2InputField", "Float3InputField", "Float4InputField"],
    },
    {
        path: "game-ui/editor/widgets/fields/time-slider-field.tsx",
        components: ["TimeSliderField"],
    },
    {
        path: "game-ui/editor/widgets/fields/bounds-field.tsx",
        components: [
            "Bounds1InputField",
            "Bounds1SliderField",
            "Bounds2InputField",
            "Bounds3InputField",
            "TimeBoundsSliderField",
        ],
    },
    {
        path: "game-ui/debug/widgets/fields/slider-field.tsx",
        components: ["Float2SliderField", "Float3SliderField", "Float4SliderField"],
    },
    {
        path: "game-ui/editor/widgets/fields/bezier4x3-field.tsx",
        components: ["Bezier4x3Field"],
    },
    {
        path: "game-ui/editor/widgets/fields/ranged-slider-field.tsx",
        components: ["RangedSliderField"],
    },
    {
        path: "game-ui/editor/widgets/fields/string-input-field.tsx",
        components: ["StringInputField"],
    },
    {
        path: "game-ui/editor/widgets/fields/color-field.tsx",
        components: ["ColorField"],
    },
    {
        path: "game-ui/editor/widgets/fields/gradient-slider-field.tsx",
        components: ["GradientSliderField"],
    },
    {
        path: "game-ui/editor/widgets/fields/animation-curve-field/animation-curve-field.tsx",
        components: ["AnimationCurveField"],
    },
    {
        path: "game-ui/editor/widgets/fields/enum-field.tsx",
        components: ["EnumField", "FlagsField"],
    },
    {
        path: "game-ui/editor/widgets/fields/popup-value-field.tsx",
        components: ["PopupValueField"],
    },
    {
        path: "game-ui/editor/widgets/fields/dropdown-field.tsx",
        components: ["DropdownField"],
    },
    {
        path: "game-ui/editor/widgets/fields/directory-picker-button.tsx",
        components: ["DirectoryPickerButton"],
    },
    {
        path: "game-ui/editor/widgets/fields/seasons-field/seasons-field.tsx",
        components: ["SeasonsField"],
    },
    {
        path: "game-ui/editor/widgets/image/image-field.tsx",
        components: ["ImageField"],
    },
    {
        path: "game-ui/common/tooltip/tooltip-renderer/tooltip-renderer.tsx",
        components: ["TooltipRenderer", "BoundTooltipGroup", "tooltipComponents"],
    },
    {
        path: "game-ui/common/image/tinted-icon.tsx",
        components: ["TintedIcon"],
    },
    {
        path: "game-ui/common/localization/localized-number.tsx",
        components: ["LocalizedNumber"],
    },
];

// NetworkTools-specific vanilla themes beyond the shared base set.
const extraThemePaths: ThemePath[] = [
    {
        path: "game-ui/common/image/tinted-icon.module.scss",
        name: "tintedIcon",
    },
    {
        path: "game-ui/common/tooltip/tooltip-renderer/tooltip-renderer.module.scss",
        name: "tooltipRenderer",
    },
];

export const initialize = (moduleRegistry: ModuleRegistry) =>
    initializeBase(moduleRegistry, extraModulePaths, extraThemePaths);
