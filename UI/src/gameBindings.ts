import { trigger } from "cs2/api";
import mod from "../mod.json";
import { TwoWayBinding } from "utils/bidirectionalBinding";
import { PrefabData, ZoneData } from "types";

export const GAME_BINDINGS = {
    TOOL_ENABLED: new TwoWayBinding<boolean>("TOOL_ENABLED", false),
    ALLOW_SPAWNING_INFO_SECTION: new TwoWayBinding<boolean>("ALLOW_SPAWNING_INFO_SECTION", false),
    BLOCK_WIDTH: new TwoWayBinding<number>("BLOCK_WIDTH", 2),
    BLOCK_DEPTH: new TwoWayBinding<number>("BLOCK_DEPTH", 2),
    PREFAB_DATA: new TwoWayBinding<PrefabData>("PREFAB_DATA", {
        name: undefined,
        thumbnail: undefined,
    }),
    ZONE_DATA: new TwoWayBinding<ZoneData[]>("ZONE_DATA", []),
    RENDER_PARCELS: new TwoWayBinding<boolean>("RENDER_PARCELS", true),
    ALLOW_SPAWNING: new TwoWayBinding<boolean>("ALLOW_SPAWNING", true),
    SNAP_ROADSIDE: new TwoWayBinding<boolean>("SNAP_ROADSIDE", true),
    SNAP_SPACING: new TwoWayBinding<number>("SNAP_SPACING", 0),
    ENABLE_TOOL_BUTTONS: new TwoWayBinding<boolean>("ENABLE_TOOL_BUTTONS", true),
    ZONE: new TwoWayBinding<number>("ZONE", 0),
    MODAL__FIRST_LAUNCH: new TwoWayBinding<boolean>("MODAL__FIRST_LAUNCH", false),
};

export const GAME_TRIGGERS = {
    ADJUST_BLOCK_SIZE: (action: string) => {
        console.log("ADJUST_BLOCK_SIZE", action);
        trigger(mod.id, "TRIGGER:ADJUST_BLOCK_SIZE", action);
    },
    MODAL_DISMISS: (modal: string) => {
        trigger(mod.id, "TRIGGER:MODAL_DISMISS", modal);
    },
};
