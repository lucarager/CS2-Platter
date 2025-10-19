import React from "react";
interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

export const ParcelInfoPanelComponent = (componentList: any) => {
    componentList["Platter.Systems.P_ParcelInfoPanelSystem"] = (e: InfoSectionComponent) => {
        return <h1>Parcel</h1>;
    };

    return componentList as any;
};
