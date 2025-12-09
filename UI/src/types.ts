import { Entity } from "cs2/bindings";

export type PrefabData = {
    name: string | undefined;
    thumbnail: string | undefined;
};

export type ParcelUIData = {
    name: string | undefined;
    zoning: string | undefined;
};

export type AssetPackData = {
    name: string;
    icon: string;
    entity: Entity;
};

export type ZoneData = {
    name: string;
    thumbnail: string;
    areaType: string;
    group: Entity;
    index: number;
    assetPacks: Entity[];
};

export type ZoneGroupData = {
    name: string;
    icon: string;
    entity: Entity;
};

export enum SnapMode {
    None,
    ZoneSide,
    RoadSide,
}
