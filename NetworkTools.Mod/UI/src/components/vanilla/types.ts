import { LocalizedString } from "cs2/bindings";

// Re-export the shared vanilla base types (IVanillaComponents/Themes/Focus, Vanilla*Props,
// Page*Props) so existing NetworkTools imports keep resolving. The shared IVanillaComponents
// carries an index signature, so the NetworkTools-only components registered in Components.tsx
// (ToggleField, EnumField, tooltipComponents, ...) remain accessible through VC without needing
// to be declared here.
export * from "vanilla/types";

// --- NetworkTools-specific types (not part of the shared base) ---

export type Widget = {
    path: string;
    children: Tooltip[];
};

export type Tooltip = Widget & {
    props: {
        __Type: TooltipType;
        disbled: boolean;
        hidden: boolean;
        color: string;
        label: LocalizedString;
        icon?: string;
        value?: number;
        unit?: string;
        signed?: boolean;
    };
};

export enum Alignment {
    Start,
    Center,
    End,
}

export type TooltipGroup = Widget & {
    props: {
        disbled: boolean;
        hidden: boolean;
        position: {
            x: number;
            y: number;
        };
        horizontalAlignment: Alignment;
        verticalAlignment: Alignment;
        category: string;
    };
};

export enum TooltipType {
    "Game.UI.Tooltip.NumberTooltip",
    "Game.UI.Tooltip.ProgressTooltip",
    "Game.UI.Tooltip.StringTooltip",
    "Game.UI.Tooltip.NameTooltip",
    "Game.UI.Tooltip.NotificationTooltip",
    "Game.UI.Tooltip.ZoningEvaluationTooltip",
    "Game.UI.Tooltip.InputHintTooltip",
    "NetworkTools.UI.NT_InputHintTooltip",
}

export interface ValueFieldProps {
    id?: string;
    label?: string | JSX.Element;
    warning?: boolean;
    theme?: any;
    disabled?: boolean;
    className?: string;
    children?: any;
    onClick?: () => void;
}

export interface ToggleFieldProps {
    label: string;
    value: boolean;
    disabled?: boolean;
    tooltip?: string | null;
    onChange: (value: boolean) => void;
}

export interface IntInputFieldProps {
    label: string;
    value: number;
    min?: number;
    max?: number;
    disabled?: boolean;
    tooltip?: string | null;
    onChange: (value: number) => void;
    onChangeStart?: () => void;
    onChangeEnd?: () => void;
}

export interface IntSliderFieldProps {
    label: string;
    value: number;
    min: number;
    max: number;
    disabled?: boolean;
    tooltip?: string | null;
    onChange: (value: number) => void;
    onChangeStart?: () => void;
    onChangeEnd?: () => void;
}

export interface FloatInputFieldProps {
    label: string;
    value: number;
    min?: number;
    max?: number;
    fractionDigits?: number;
    disabled?: boolean;
    tooltip?: string | null;
    onChange: (value: number) => void;
    onChangeStart?: () => void;
    onChangeEnd?: () => void;
}

export interface FloatSliderFieldProps {
    label: string;
    value: number;
    min: number;
    max: number;
    onChange: (x: number) => void;
    fractionDigits?: number;
    disabled?: boolean;
    tooltip?: string | JSX.Element | null;
    onChangeStart?: () => void;
    onChangeEnd?: () => void;
    className?: any;
}

export interface VectorInputFieldProps {
    label: string;
    value: any;
    min?: any;
    max?: any;
    disabled?: boolean;
    onChange: (value: any) => void;
}

export interface VectorSliderFieldProps {
    label: string;
    value: any;
    min?: any;
    max?: any;
    step?: number;
    fractionDigits?: number;
    disabled?: boolean;
    onChange: (value: any) => void;
}

export interface BoundsFieldProps {
    label: string;
    value: any;
    allowMinGreaterMax?: boolean;
    disabled?: boolean;
    onChange: (value: any) => void;
    [key: string]: any;
}

export interface RangedSliderFieldProps {
    label: string;
    iconSrc?: string;
    value: number;
    min: number;
    max: number;
    fractionDigits?: number;
    disabled?: boolean;
    tooltip?: string | null;
    onChange: (value: number) => void;
    onChangeStart?: () => void;
    onChangeEnd?: () => void;
}

export interface StringInputFieldProps {
    value: string;
    disabled?: boolean;
    onChange: (value: string) => void;
    onChangeStart?: () => void;
    onChangeEnd?: () => void;
    className?: string;
    multiline?: boolean;
    maxLength?: number;
}

export interface ColorFieldProps {
    label: string;
    value: any;
    showAlpha?: boolean;
    disabled?: boolean;
    onChange: (value: any) => void;
}

export interface GradientSliderFieldProps {
    gradient: any;
    iconSrc?: string;
    label: string;
    value: number;
    min: number;
    max: number;
    tooltip?: string | null;
    onChange: (value: number) => void;
}

export interface AnimationCurveFieldProps {
    label: string;
    value: any;
    disabled?: boolean;
    hidePreview?: boolean;
    expanded?: boolean;
    initialEditing?: boolean;
    autoUpdateBounds?: boolean;
    bounds?: any;
    smoothenOnMove?: boolean;
    canAddKeyframes?: boolean;
    wrapMode?: any;
    parseKeyframe?: (oldKeyframe: any, newKeyframe: any, index: number, keys: any[], curveIndex: number) => any;
    onAddKeyframe?: (keyframe: any) => void;
    onMoveKeyframe?: (keyframe: any) => void;
    onRemoveKeyframe?: (keyframe: any) => void;
    onSetKeyframes?: (keyframes: any[]) => void;
}

export interface EnumFieldProps {
    label: string;
    value: number;
    enumMembers: { value: number; displayName: string }[];
    disabled?: boolean;
    tooltip?: string | null;
    onChange: (value: number) => void;
}

export interface FlagsFieldProps {
    label: string;
    value: number;
    enumMembers: { value: number; displayName: string }[];
    disabled?: boolean;
    onChange: (value: number) => void;
}

export interface PopupValueFieldProps {
    label: string;
    value: any;
    expanded: boolean;
    disabled?: boolean;
    tooltip?: string | null;
    onExpandedChange: (expanded: boolean) => void;
}

export interface DropdownFieldProps {
    items: { value: any; displayName: string }[];
    value: any;
    disabled?: boolean;
    onChange: (value: any) => void;
}

export interface DirectoryPickerButtonProps {
    label: string;
    value: string;
    disabled?: boolean;
    tooltip?: string | null;
    className?: string;
    theme?: any;
    onOpenDirectoryBrowser: () => void;
}

export interface SeasonsFieldProps {
    value: any;
    seasons: any[];
    label?: string;
    [key: string]: any;
}

export interface ImageFieldProps {
    uri: string;
    label?: string;
    tooltip?: string | null;
}
