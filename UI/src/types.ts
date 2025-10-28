export type PrefabData = {
    name: string | undefined;
    thumbnail: string | undefined;
};

export type ParcelUIData = {
    name: string | undefined;
    zoning: string | undefined;
};

export type ZoneData = {
    name: string;
    thumbnail: string;
    category: string;
    index: number;
};

export enum SnapMode {
    None,
    ZoneSide,
    RoadSide,
}
