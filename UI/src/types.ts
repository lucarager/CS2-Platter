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
    category: string;
    index: number;
    assetPacks: Entity[];
};

export enum SnapMode {
    None,
    ZoneSide,
    RoadSide,
}
