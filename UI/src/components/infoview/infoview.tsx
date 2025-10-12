interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

export const SelectedInfoPanelComponent = (componentList: any) => {
    componentList["Platter.Systems.P_SelectedInfoPanelSystem"] = (e: InfoSectionComponent) => {
        return <></>;
    };

    return componentList as any;
};
