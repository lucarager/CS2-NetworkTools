import { trigger } from "cs2/api";
import mod from "../mod.json";
import { TwoWayBinding } from "utils/bidirectionalBinding";
import { Entity } from "cs2/bindings";

export type ToolUIData = {
    Id: string;
    DisplayName: string;
    Icon: string;
    Description: string;
    Active: boolean;
    Index: number;
    PrefabId: string;
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
export enum ShapeTransformTemplate {
    Preserve = 0,
    SlopeLinear = 1,
    SlopeEaseInOut = 2,
    SlopeParabolic = 3,
    CurveStraighten = 4,
    CurveSmooth = 5,
}

export type ShapeConfigData = {
    template: ShapeTransformTemplate;
    smoothingFactor: number;
    easeInLength: number;
    easeOutLength: number;
    archHeight: number;
    archPosition: number;
};
export const DEFAULT_SHAPE_CONFIG: ShapeConfigData = {
    template: ShapeTransformTemplate.Preserve,
    smoothingFactor: 0,
    easeInLength: 0,
    easeOutLength: 0,
    archHeight: 0,
    archPosition: 0,
};

export const GAME_BINDINGS = {
    UI_DATA: new TwoWayBinding<ToolUIData[]>("UI_DATA", []),
    SELECTED_ENTITIES: new TwoWayBinding<ToolSelectionData[]>("SELECTED_ENTITIES", []),
    SELECTED_PREFAB: new TwoWayBinding<string>("SELECTED_PREFAB", ""),
    PANEL_OPEN: new TwoWayBinding<boolean>("PANEL_OPEN", false),
    SHAPE_CONFIG: new TwoWayBinding<ShapeConfigData>("SHAPE_CONFIG", DEFAULT_SHAPE_CONFIG),
};

export const GAME_TRIGGERS = {
    SELECT_TOOL: (tool: string) => {
        trigger(mod.id, "TRIGGER:SELECT_TOOL", tool);
    },
    APPLY_SLOPE: () => {
        trigger(mod.id, "TRIGGER:APPLY_SLOPE");
    },
    APPLY_TRANSFORM: () => {
        trigger(mod.id, "TRIGGER:APPLY_TRANSFORM");
    },
};
