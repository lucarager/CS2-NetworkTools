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
    FloatSliderField: React.FC<FLoatSliderProps>;
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

export interface FLoatSliderProps {
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
