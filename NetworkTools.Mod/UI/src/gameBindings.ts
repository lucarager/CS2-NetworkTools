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

// Snap options (bitflags)
export enum SnapOption {
    None = 0,
    ZoneGrid = 1 << 0,
    MidPoint = 1 << 1,
    ExistingGeometry = 1 << 2,
    ObjectSide = 1 << 3,
    GuideLines = 1 << 4,
    All = ZoneGrid | MidPoint | ExistingGeometry | ObjectSide | GuideLines,
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

export type DistanceUnits = "Meters" | "Units";

export const GAME_BINDINGS = {
    UI_DATA: new TwoWayBinding<ToolUIData[]>("UI_DATA", []),
    SELECTED_ENTITIES: new TwoWayBinding<ToolSelectionData[]>("SELECTED_ENTITIES", []),
    SELECTED_PREFAB: new TwoWayBinding<string>("SELECTED_PREFAB", ""),
    PANEL_OPEN: new TwoWayBinding<boolean>("PANEL_OPEN", false),
    DISTANCE_UNIT: new TwoWayBinding<DistanceUnits>("DISTANCE_UNIT", "Meters"),
    AVAILABLE_SNAPS: new TwoWayBinding<number>("AVAILABLE_SNAPS", SnapOption.None),
    SELECTED_SNAPS: new TwoWayBinding<number>("SELECTED_SNAPS", SnapOption.None),
    AVAILABLE_TARGETS: new TwoWayBinding<number>("AVAILABLE_TARGETS", TargetOption.All),
    SELECTED_TARGETS: new TwoWayBinding<number>("SELECTED_TARGETS", TargetOption.All),
    AVAILABLE_VIEWS: new TwoWayBinding<number>("AVAILABLE_VIEWS", ViewOption.All),
    SELECTED_VIEWS: new TwoWayBinding<number>("SELECTED_VIEWS", ViewOption.None),
    PS_SELECTED_TYPE: new TwoWayBinding<number>("PS:SELECTED_TYPE", PrefabType.Road),
    PS_DATA: new TwoWayBinding<PrefabSelectionEntry[]>("PS:DATA", []),
    PS_CACHED_PREFAB: new TwoWayBinding<PrefabSelectionEntry[]>("PS:CACHED_PREFAB", []),
};

export const GAME_TRIGGERS = {
    SELECT_TOOL: (tool: string) => {
        trigger(mod.id, "TRIGGER:SELECT_TOOL", tool);
    },
    REQUEST_APPLY: () => {
        trigger(mod.id, "TRIGGER:REQUEST_APPLY");
    },
    PS_SELECT: (key: string, entity: Entity) => {
        trigger(mod.id, "TRIGGER:PS:SELECT", key, entity);
    },
    RESET_PARAM: (key: string) => {
        trigger(mod.id, "TRIGGER:RESET_PARAM", key);
    },
    RESET_TOOL: () => {
        trigger(mod.id, "TRIGGER:RESET_TOOL");
    },
};
