import { trigger } from "cs2/api";
import mod from "../mod.json";
import { BidirectionalBinding as TwoWayBinding } from "utils/bidirectionalBinding";

export type PrefabData = {
    name: string | undefined;
    thumbnail: string | undefined;
};

export type ZoneData = {
    name: string;
    thumbnail: string;
    index: number;
};

export const $bindings = {
    toolEnabled: new TwoWayBinding<boolean>("TOOL_ENABLED", false),
    toolMode: new TwoWayBinding<number>("TOOL_MODE", 0),
    infoSectionAllowSpawningToggle: new TwoWayBinding<boolean>(
        "ALLOW_SPAWNING_INFO_SECTION",
        false,
    ),
    blockWidth: new TwoWayBinding<number>("BLOCK_WIDTH", 2),
    blockDepth: new TwoWayBinding<number>("BLOCK_DEPTH", 2),
    zone: new TwoWayBinding<number>("ZONE", 0),
    spacing: new TwoWayBinding<number>("RE_SPACING", 0),
    offset: new TwoWayBinding<number>("RE_OFFSET", 0),
    sides: new TwoWayBinding<boolean[]>("RE_SIDES", [true, true, false, false]),
    prefab: new TwoWayBinding<PrefabData>("PREFAB_DATA", {
        name: undefined,
        thumbnail: undefined,
    }),
    zoneData: new TwoWayBinding<ZoneData[]>("ZONE_DATA", []),
};

export const triggers = {
    adjustBlockSize: (action: string) => {
        trigger(mod.id, "TRIGGER:ADJUST_BLOCK_SIZE", action);
    },
};
