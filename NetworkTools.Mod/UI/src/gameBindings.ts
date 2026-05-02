import { trigger } from "cs2/api";
import mod from "../mod.json";
import { TwoWayBinding } from "utils/bidirectionalBinding";
import { Entity } from "cs2/bindings";

export type ToolUIData = {
    Id: string;
    DisplayName: string;
    Icon: string;
    Description: string;
    Category: string;
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

// Snap options (bitflags)
export enum SnapOption {
    None = 0,
    ZoneGrid = 1 << 0,
    MidPoint = 1 << 1,
    All = ZoneGrid | MidPoint,
}

// Target options (bitflags)
export enum TargetOption {
    None = 0,
    Road = 1 << 0,
    Path = 1 << 1,
    Rail = 1 << 2,
    Waterway = 1 << 3,
    InvisiblePath = 1 << 4,
    All = Road | Path | Rail | Waterway | InvisiblePath,
}

// View options (bitflags)
export enum ViewOption {
    None = 0,
    Underground = 1 << 0,
    ZoneGrid = 1 << 1,
    InvisibleNetworks = 1 << 2,
    All = Underground | ZoneGrid | InvisibleNetworks,
}

export enum PrefabType {
    Road = 0,
    Path = 1,
    Rail = 2,
    Waterway = 3,
    NetLane = 4,
}

export type PrefabSelectionEntry = {
    Entity: Entity;
    Name: string;
    Icon: string;
    Type: PrefabType;
};

export type NetPrefabData = {
    Entity: Entity;
    Thumbnail: string;
    Name: string;
};

export const EMPTY_NET_PREFAB_DATA: NetPrefabData = {
    Entity: { index: 0, version: 0 },
    Thumbnail: "",
    Name: "",
};

export const GAME_BINDINGS = {
    UI_DATA: new TwoWayBinding<ToolUIData[]>("UI_DATA", []),
    SELECTED_ENTITIES: new TwoWayBinding<ToolSelectionData[]>("SELECTED_ENTITIES", []),
    SELECTED_PREFAB: new TwoWayBinding<string>("SELECTED_PREFAB", ""),
    PANEL_OPEN: new TwoWayBinding<boolean>("PANEL_OPEN", false),
    SHAPE_CONFIG: new TwoWayBinding<ShapeConfigData>("SHAPE_CONFIG", DEFAULT_SHAPE_CONFIG),
    SELECTED_NET_PREFAB: new TwoWayBinding<NetPrefabData>(
        "SELECTED_NET_PREFAB",
        EMPTY_NET_PREFAB_DATA,
    ),
    AVAILABLE_SNAPS: new TwoWayBinding<number>("AVAILABLE_SNAPS", SnapOption.None),
    SELECTED_SNAPS: new TwoWayBinding<number>("SELECTED_SNAPS", SnapOption.None),
    AVAILABLE_TARGETS: new TwoWayBinding<number>("AVAILABLE_TARGETS", TargetOption.All),
    SELECTED_TARGETS: new TwoWayBinding<number>("SELECTED_TARGETS", TargetOption.All),
    AVAILABLE_VIEWS: new TwoWayBinding<number>("AVAILABLE_VIEWS", ViewOption.All),
    SELECTED_VIEWS: new TwoWayBinding<number>("SELECTED_VIEWS", ViewOption.None),
    PS_SELECTED_TYPE: new TwoWayBinding<number>("PS:SELECTED_TYPE", PrefabType.Road),
    PS_DATA: new TwoWayBinding<PrefabSelectionEntry[]>("PS:DATA", []),
};

export const GAME_TRIGGERS = {
    SELECT_TOOL: (tool: string) => {
        trigger(mod.id, "TRIGGER:SELECT_TOOL", tool);
    },
    REQUEST_APPLY: () => {
        trigger(mod.id, "TRIGGER:REQUEST_APPLY");
    },
    PS_SELECT: (entity: Entity) => {
        trigger(mod.id, "TRIGGER:PS:SELECT", entity);
    },
    RESET_PARAM: (key: string) => {
        trigger(mod.id, "TRIGGER:RESET_PARAM", key);
    },
    RESET_TOOL: () => {
        trigger(mod.id, "TRIGGER:RESET_TOOL");
    },
};
