import React from "react";
import { VC, VF } from "components/vanilla/Components";
import { useValue } from "cs2/api";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useLocalization } from "cs2/l10n";

export const BuildingInfoPanelComponent = (componentList: any) => {
    const Component: React.FC = () => {
        const parcelBinding = useValue(GAME_BINDINGS.INFOPANEL_BUILDING_PARCEL_ENTITY.binding);
        const { translate } = useLocalization();

        return (
            <VC.InfoSection focusKey={VF.FOCUS_DISABLED} disableFocus={true}>
                <VC.InfoRow
                    left={"Parcel"}
                    link={
                        <VC.InfoLink
                            tooltip={
                                translate("PlatterMod.UI.Tooltip.Infopanel.InspectParcel") || ""
                            }
                            onSelect={() => {
                                GAME_TRIGGERS.INFOPANEL_SELECT_PARCEL_ENTITY(parcelBinding);
                            }}>
                            {translate("PlatterMod.UI.Button.Infopanel.InspectParcel")}
                        </VC.InfoLink>
                    }
                    uppercase={true}
                    disableFocus={true}
                    subRow={false}></VC.InfoRow>
            </VC.InfoSection>
        );
    };

    componentList["Platter.Systems.P_BuildingInfoPanelSystem"] = Component;

    return componentList as any;
};
