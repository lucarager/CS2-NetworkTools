import React, { PropsWithChildren } from "react";
import { LocalizedString, UniqueFocusKey } from "cs2/bindings";
import { HTMLAttributes } from "react";
import { InfoRowProps, InfoSectionProps } from "cs2/ui";
import { LocalizedNumberProps, LocComponent } from "cs2/l10n";

export interface IVanillaComponents {
    Section: React.FC<VanillaSectionProps>;
    StepToolButton: React.FC<VanillaStepToolButtonProps>;
    TabBar: React.FC<VanillaTabBarProps>;
    Checkbox: React.FC<VanillaCheckboxProps>;
    ToolButton: React.FC<VanillaToolButtonProps>;
    InfoSection: React.FC<PropsWithChildren<InfoSectionProps>>;
    InfoRow: React.FC<PropsWithChildren<InfoRowProps>>;
    InfoLink: React.FC<PropsWithChildren<VanillaInfoLinkProps>>;
    PageSelector: React.FC<PageSelectorProps>;
    Page: React.FC<any>;
    PageSwitcher: React.FC<PropsWithChildren<PageSwitcherProps>>;
    ValueField: React.FC<ValueFieldProps>;
    LocalizedValueField: React.FC<any>;
    ToggleField: React.FC<ToggleFieldProps>;
    IntInputField: React.FC<IntInputFieldProps>;
    IntSliderField: React.FC<IntSliderFieldProps>;
    Int2InputField: React.FC<VectorInputFieldProps>;
    Int3InputField: React.FC<VectorInputFieldProps>;
    Int4InputField: React.FC<VectorInputFieldProps>;
    UIntInputField: React.FC<IntInputFieldProps>;
    UIntSliderField: React.FC<IntSliderFieldProps>;
    TimeSliderField: React.FC<IntSliderFieldProps>;
    TimeBoundsSliderField: React.FC<BoundsFieldProps>;
    FloatInputField: React.FC<FloatInputFieldProps>;
    FloatSliderField: React.FC<FloatSliderFieldProps>;
    Float2InputField: React.FC<VectorInputFieldProps>;
    Float2SliderField: React.FC<VectorSliderFieldProps>;
    Float3InputField: React.FC<VectorInputFieldProps>;
    Float3SliderField: React.FC<VectorSliderFieldProps>;
    Float4InputField: React.FC<VectorInputFieldProps>;
    Float4SliderField: React.FC<VectorSliderFieldProps>;
    Bounds1InputField: React.FC<BoundsFieldProps>;
    Bounds1SliderField: React.FC<BoundsFieldProps>;
    Bounds2InputField: React.FC<BoundsFieldProps>;
    Bounds3InputField: React.FC<BoundsFieldProps>;
    Bezier4x3Field: React.FC<any>;
    RangedSliderField: React.FC<RangedSliderFieldProps>;
    StringInputField: React.FC<StringInputFieldProps>;
    ColorField: React.FC<ColorFieldProps>;
    GradientSliderField: React.FC<GradientSliderFieldProps>;
    AnimationCurveField: React.FC<AnimationCurveFieldProps>;
    EnumField: React.FC<EnumFieldProps>;
    FlagsField: React.FC<FlagsFieldProps>;
    PopupValueField: React.FC<PropsWithChildren<PopupValueFieldProps>>;
    DropdownField: React.FC<DropdownFieldProps>;
    DirectoryPickerButton: React.FC<DirectoryPickerButtonProps>;
    SeasonsField: React.FC<SeasonsFieldProps>;
    ImageField: React.FC<ImageFieldProps>;
    focusEntity: any;
    tooltipComponents: {
        [x in TooltipType]: {
            ({ props }: { props: any }): any;
            displayName: any;
        };
    };
    LocalizedNumber: LocComponent<LocalizedNumberProps>;
    [key: string]: React.FC<any> | any;
}

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

export interface PageSelectorProps {
    pages: number;
    selected: number;
    onSelect?: (x: number) => any;
}

export interface PageSwitcherProps {
    activePage: number;
    transitionStyles?: any;
    className?: any;
}

export interface IVanillaThemes {
    toolButtonTheme: Record<"button", string>;
    mouseToolOptionsTheme: Record<"startButton" | "numberField" | "endButton", string>;
    dropdownTheme: Record<"dropdownItem" | "dropdownToggle", string>;
    checkboxTheme: Record<"label", string>;
    toolbarFeatureButton: Record<"toolbarFeatureButton" | "button", string>;
    panel: Record<string, string>;
    pageSelector: Record<string, string>;
    whatsNewPage: Record<string, string>;
    [key: string]: Record<string, string>;
}

export interface IVanillaFocus {
    FOCUS_DISABLED: UniqueFocusKey;
    FOCUS_AUTO: UniqueFocusKey;
}

export type VanillaToolButtonProps = {
    focusKey?: UniqueFocusKey | null;
    src: string;
    selected?: boolean;
    multiSelect?: boolean;
    disabled?: boolean;
    tooltip?: string | JSX.Element | null;
    selectSound?: any;
    uiTag?: string;
    className?: string;
    children?: string | JSX.Element | JSX.Element[];
    onSelect?: (x: any) => any;
} & HTMLAttributes<any>;

export type VanillaStepToolButtonProps = {
    focusKey?: UniqueFocusKey | null;
    selectedValue: number;
    values: number[];
    tooltip?: string | null;
    uiTag?: string;
    onSelect?: (x: any) => any;
} & HTMLAttributes<any>;

export type VanillaTabBarProps = any;

export type VanillaSectionProps = {
    title?: string | null;
    uiTag?: string;
    children: string | JSX.Element | JSX.Element[];
    focusKey?: UniqueFocusKey | null;
};

export type VanillaCheckboxProps = {
    checked?: boolean;
    disabled?: boolean;
    theme?: any;
    className?: string;
    [key: string]: any;
};

export type VanillaInfoLinkProps = {
    icon?: string;
    tooltip: string;
    uppercase?: boolean;
    onSelect?: (x: any) => any;
};
