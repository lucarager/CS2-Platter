import { VC, VF } from "components/vanilla/Components";
import { bindTriggerWithArgs, trigger } from "cs2/api";
import React from "react";
interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

export const BuildingInfoPanelComponent = (componentList: any) => {
    var binding: any = bindTriggerWithArgs("selectedInfo", "selectedEntity");
    componentList["Platter.Systems.P_BuildingInfoPanelSystem"] = (e: InfoSectionComponent) => {
        return (
            <VC.InfoSection focusKey={VF.FOCUS_DISABLED} disableFocus={true}>
                <VC.InfoRow
                    left={"Parcel"}
                    link={
                        <VC.InfoLink tooltip="Hey" onSelect={() => {binding({__Type: 'Unity.Entities.Entity', index: 49323, version: 15})}}>
                            _ParcelName_
                        </VC.InfoLink>
                    }
                    uppercase={true}
                    disableFocus={true}
                    subRow={false}></VC.InfoRow>
            </VC.InfoSection>
        );
    };

    return componentList as any;
};
