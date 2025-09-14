import React from "react";
import { Button } from "cs2/ui";
import { ModuleRegistryExtend } from "cs2/modding";
import { GAME_BINDINGS } from "modBindings";
import styles from "./toolButton.module.scss";
import { useValue } from "cs2/api";
import { VanillaThemes } from "../vanilla/Components";

export const buttonId = "platterToolButton";

export const ToolButton: ModuleRegistryExtend = (Component) => {
    const c = (props: any) => {
        // eslint-disable-next-line react-hooks/rules-of-hooks
        const toolEnabledBinding = useValue(GAME_BINDINGS.TOOL_ENABLED.binding);
        const iconSrc = "Media/Game/Icons/Zones.svg";
        const { children, ...otherProps } = props || {};

        return (
            <>
                <Button
                    id={buttonId}
                    className={VanillaThemes.toolbarFeatureButton.button + " " + styles.Icon}
                    src={iconSrc}
                    variant="icon"
                    onSelect={() => GAME_BINDINGS.TOOL_ENABLED.set(!toolEnabledBinding)}></Button>

                <Component {...otherProps}>{children}</Component>
            </>
        );
    };

    return c;
};
