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

export type SlopeConfigData = {
    template: "parabolic" | "easeinout" | "linear";
    easeInLength: number;
    easeOutLength: number;
    archHeight: number;
    archPosition: number;
};

export const GAME_BINDINGS = {
    UI_DATA: new TwoWayBinding<ToolUIData[]>("UI_DATA", []),
    SELECTED_ENTITIES: new TwoWayBinding<ToolSelectionData[]>("SELECTED_ENTITIES", []),
    SELECTED_PREFAB: new TwoWayBinding<string>("SELECTED_PREFAB", ""),
    SLOPE_CONFIG: new TwoWayBinding<SlopeConfigData>("SLOPE_CONFIG", {
        template: "linear",
        easeInLength: 0.25,
        easeOutLength: 0.25,
        archHeight: 0.5,
        archPosition: 0.5,
    }),
};

export const GAME_TRIGGERS = {
    SELECT_TOOL: (tool: string) => {
        trigger(mod.id, "TRIGGER:SELECT_TOOL", tool);
    },
    APPLY_SLOPE: (mode: string) => {
        trigger(mod.id, "TRIGGER:APPLY_SLOPE", mode);
    },
};
