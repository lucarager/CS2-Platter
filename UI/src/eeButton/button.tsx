import React from "react";
import { Button } from "cs2/ui";
import { Theme } from "cs2/bindings";
import { getModule, ModuleRegistryExtend } from "cs2/modding";
import { $bindings } from "modBindings";

// Getting the vanilla theme css for compatibility
const ToolBarButtonTheme: Theme | any = getModule(
    "game-ui/game/components/toolbar/components/feature-button/toolbar-feature-button.module.scss",
    "classes",
);

export const buttonId = "eeToolButton";

export const EEToolButton: ModuleRegistryExtend = (Component) => {
    const c = (props: any) => {
        const toolEnabledBinding = $bindings.EEtoolEnabled.use();
        const iconSrc = "Media/Game/Icons/Roads.svg";
        const { children, ...otherProps } = props || {};

        return (
            <>
                <Button
                    id={buttonId}
                    className={ToolBarButtonTheme.button}
                    src={iconSrc}
                    variant="icon"
                    onSelect={() => $bindings.EEtoolEnabled.set(!toolEnabledBinding)}></Button>

                <Component {...otherProps}></Component>
            </>
        );
    };

    return c;
};
