import { useState } from "react";
import { Button } from "cs2/ui";
import { bindValue, trigger, useValue } from "cs2/api";
import { game, tool, Theme } from "cs2/bindings";
import { getModule, ModuleRegistryExtend } from "cs2/modding";
import mod from "../../mod.json";
import styles from "./button.module.scss";
import { events, triggers } from "modBindings";

// Getting the vanilla theme css for compatibility
const ToolBarButtonTheme: Theme | any = getModule(
    "game-ui/game/components/toolbar/components/feature-button/toolbar-feature-button.module.scss",
    "classes"
);

export const buttonId = "platterToolButton";

export const ToolButton: ModuleRegistryExtend = (Component) =>
    {
        return (props) => {
            const iconSrc = "Media/Game/Icons/Zones.svg";
            const { children, ...otherProps } = props || {};

            return (
                <>
                    <Button
                        id={buttonId}
                        className ={ToolBarButtonTheme.button + " " + styles.Icon}
                        src={iconSrc}
                        variant="icon"
                        onSelect={() => triggers.toggleTool()}>
                    </Button>

                    <Component {...otherProps}></Component>
                </>
            );
        }
    }
