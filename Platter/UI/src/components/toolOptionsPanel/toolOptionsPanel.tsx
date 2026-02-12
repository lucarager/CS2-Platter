import React, { useEffect, useRef } from "react";
import { ModuleRegistryExtend } from "cs2/modding";
import { VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import styles from "./toolOptionsPanel.module.scss";
import { c } from "utils/classes";
import { SnapMode } from "types";
import { useRenderTracker, useValueWrap } from "../../debug";
import { FocusDisabled } from "cs2/input";
import { PrezoningSection } from "./prezoningSection";
import { SnapRoadsideSection } from "./snapRoadsideSection";
import { ParcelWidthSection } from "./parcelWidthSection";
import { ParcelDepthSection } from "./parcelDepthSection";
import { SnapModeSection } from "./snapModeSection";
import { ToolViewmodeSection } from "./toolViewmodeSection";

export type BlockControlProps = Record<string, never>;

export const ToolModes = [
    {
        title: "Plop",
        icon: "",
    },
    {
        title: "Road Mode",
        icon: "",
    },
];

export const PlatterToolOptionsPanel: ModuleRegistryExtend = (Component: any) => {
    const PlatterToolOptionsPanelComponentWrapper = (props: any) => {
        const enabledBinding = useValueWrap(
            GAME_BINDINGS.ENABLE_TOOL_BUTTONS.binding,
            "EnableToolButtons",
        );

        return (
            <>
                {enabledBinding && <ToolPanel />}
                <Component {...props} />
            </>
        );
    };

    return PlatterToolOptionsPanelComponentWrapper;
};

const ToolPanel = function ToolPanel() {
    useRenderTracker("ToolPanel/ToolPanel");
    const stylesheet = useRef(document.createElement("style"));
    stylesheet.current.type = "text/css";
    stylesheet.current.innerHTML = `
        .${VT.itemGrid.item.split(" ").join(".")}.selected {
            background-color: rgba(0, 0, 0, 0);
            background-image: linear-gradient(to top left, rgba(255, 213, 210, .5), rgba(253, 189, 203, .5));
        }

        .${VT.assetCategoryTabBar.assetCategoryTabBar} {
            border-bottom-color: rgba(255, 98, 182, 0.69);
        }

        .${VT.assetCategoryTabItem.button}.selected {
            background-image: linear-gradient(to top left, rgba(255, 98, 182, 0.25), rgba(253, 189, 203, .5));
        }
    `;

    useEffect(() => {
        document.head.appendChild(stylesheet.current);

        return () => {
            if (document.head.contains(stylesheet.current)) {
                document.head.removeChild(stylesheet.current);
            }
        };
    }, []);

    return (
        <FocusDisabled>
            <div className={styles.wrapper}>
                <div className={c(VT.toolOptionsPanel.toolOptionsPanel, styles.moddedSection)}>
                    <PrezoningSection />
                    <SnapRoadsideSection />
                    <ParcelWidthSection />
                    <ParcelDepthSection />
                    <SnapModeSection />
                    <ToolViewmodeSection />
                </div>
            </div>
        </FocusDisabled>
    );
};
