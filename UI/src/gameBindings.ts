import { trigger } from "cs2/api";
import mod from "../mod.json";
import { TwoWayBinding } from "utils/bidirectionalBinding";
import { Entity } from "cs2/bindings";

export type ToolUIData = {
    DisplayName: string;
    Icon: string;
    Description: string;
    Active: boolean;
    Index: number;
    ID: string;
};

export enum SelectedEntityType {
    Unknown = 0,
    Node = 1,
    Edge = 2,
}

export type ToolSelectionData = {
    Entity: Entity;
    Type: SelectedEntityType;
    Name: string;
};

// Shape template types (XZ plane transformations)
export type ShapeTemplate = "preserve" | "straighten" | "smooth" | "equalspacing";

// Slope template types (Y axis transformations)
export type SlopeTemplate = "preserve" | "linear" | "easeinout" | "parabolic";

export type ShapeConfigData = {
    template: ShapeTemplate;
    smoothingFactor: number;
};

export type SlopeConfigData = {
    template: SlopeTemplate;
    easeInLength: number;
    easeOutLength: number;
    archHeight: number;
    archPosition: number;
};

// Unified transform configuration
export type TransformConfigData = {
    shape: ShapeConfigData;
    slope: SlopeConfigData;
};

export const DEFAULT_SHAPE_CONFIG: ShapeConfigData = {
    template: "preserve",
    smoothingFactor: 0.5,
};

export const DEFAULT_SLOPE_CONFIG: SlopeConfigData = {
    template: "linear",
    easeInLength: 0.25,
    easeOutLength: 0.25,
    archHeight: 0.5,
    archPosition: 0.5,
};

export const DEFAULT_TRANSFORM_CONFIG: TransformConfigData = {
    shape: DEFAULT_SHAPE_CONFIG,
    slope: DEFAULT_SLOPE_CONFIG,
};

export const GAME_BINDINGS = {
    UI_DATA: new TwoWayBinding<ToolUIData[]>("UI_DATA", []),
    SELECTED_ENTITIES: new TwoWayBinding<ToolSelectionData[]>("SELECTED_ENTITIES", []),
    SELECTED_PREFAB: new TwoWayBinding<string>("SELECTED_PREFAB", ""),
    SLOPE_CONFIG: new TwoWayBinding<SlopeConfigData>("SLOPE_CONFIG", DEFAULT_SLOPE_CONFIG),
    SHAPE_CONFIG: new TwoWayBinding<ShapeConfigData>("SHAPE_CONFIG", DEFAULT_SHAPE_CONFIG),
    TRANSFORM_CONFIG: new TwoWayBinding<TransformConfigData>("TRANSFORM_CONFIG", DEFAULT_TRANSFORM_CONFIG),
};

export const GAME_TRIGGERS = {
    SELECT_TOOL: (tool: string) => {
        trigger(mod.id, "TRIGGER:SELECT_TOOL", tool);
    },
    APPLY_SLOPE: (mode: string) => {
        trigger(mod.id, "TRIGGER:APPLY_SLOPE", mode);
    },
    APPLY_TRANSFORM: () => {
        trigger(mod.id, "TRIGGER:APPLY_TRANSFORM");
    },
};
