import React from "react";
interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

export const BuildingInfoPanelComponent = (componentList: any) => {
    componentList["Platter.Systems.P_BuildingInfoPanelSystem"] = (e: InfoSectionComponent) => {
        return <h1>Building</h1>;
    };

    return componentList as any;
};
