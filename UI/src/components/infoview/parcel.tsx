import React from "react";
import { VC, VF } from "components/vanilla/Components";
import { useValue } from "cs2/api";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useLocalization } from "cs2/l10n";

export const ParcelInfoPanelComponent = (componentList: any) => {
    const Component: React.FC = () => {
        const dataBinding = useValue(GAME_BINDINGS.INFOPANEL_PARCEL_DATA.binding);
        const buildingDataBinding = useValue(GAME_BINDINGS.INFOPANEL_PARCEL_DATA_BUILDING.binding);
        const roadDataBinding = useValue(GAME_BINDINGS.INFOPANEL_PARCEL_DATA_ROAD.binding);
        const { translate } = useLocalization();

        return (
            <VC.InfoSection focusKey={VF.FOCUS_DISABLED} disableFocus={true}>
                <VC.InfoRow
                    left={"Parcel"}
                    right={dataBinding.name}
                    uppercase={true}
                    disableFocus={true}
                    subRow={false}></VC.InfoRow>
                <VC.InfoRow
                    left={"Zoning"}
                    right={dataBinding.zoning}
                    uppercase={true}
                    disableFocus={true}
                    subRow={false}></VC.InfoRow>
                {buildingDataBinding.index != 0 ? (
                    <VC.InfoRow
                        left={"Building"}
                        link={
                            <VC.InfoLink
                                tooltip={
                                    translate("PlatterMod.UI.Tooltip.Infopanel.InspectBuilding") ||
                                    ""
                                }
                                onSelect={() => {
                                    GAME_TRIGGERS.INFOPANEL_SELECT_PARCEL_ENTITY(
                                        buildingDataBinding,
                                    );
                                }}>
                                {translate("PlatterMod.UI.Button.Infopanel.InspectBuilding")}
                            </VC.InfoLink>
                        }
                        uppercase={true}
                        disableFocus={true}
                        subRow={false}
                    />
                ) : null}
                {roadDataBinding.index != 0 ? (
                    <VC.InfoRow
                        left={"Connected Road"}
                        link={
                            <VC.InfoLink
                                tooltip={
                                    translate("PlatterMod.UI.Tooltip.Infopanel.InspectRoad") || ""
                                }
                                onSelect={() => {
                                    GAME_TRIGGERS.INFOPANEL_SELECT_PARCEL_ENTITY(roadDataBinding);
                                }}>
                                {translate("PlatterMod.UI.Button.Infopanel.InspectRoad")}
                            </VC.InfoLink>
                        }
                        uppercase={true}
                        disableFocus={true}
                        subRow={false}
                    />
                ) : null}
            </VC.InfoSection>
        );
    };

    componentList["Platter.Systems.P_ParcelInfoPanelSystem"] = Component;

    return componentList as any;
};
