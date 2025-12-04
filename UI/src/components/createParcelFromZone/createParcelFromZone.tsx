import React from "react";
import { ModuleRegistryExtend } from "cs2/modding";
import { useValue } from "cs2/api";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import styles from "./createParcelFromZone.module.scss";
import { useLocalization } from "cs2/l10n";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { FocusDisabled } from "cs2/input";

export type BlockControlProps = Record<string, never>;

export const PlatterCreateParcelFromZone: ModuleRegistryExtend = (Component: any) => {
    const PlatterCreateParcelFromZoneComponent = (props: any) => {
        const enabledBinding = useValue(GAME_BINDINGS.ENABLE_CREATE_FROM_ZONE.binding);

        const result: JSX.Element = Component();

        if (enabledBinding) {
            result.props.children?.unshift(<Toolbar key="Platter/CreateParcelFromZone" />);
        }

        return result;
    };

    return PlatterCreateParcelFromZoneComponent;
};

const Toolbar = function Toolbar() {
    const { translate } = useLocalization();
    return (
        <div className={styles.moddedSection}>
            <FocusDisabled>
                <VC.Section title={"Platter"}>
                    <VC.ToolButton
                        focusKey={VF.FOCUS_DISABLED}
                        src="coui://platter/logo.svg"
                        onSelect={() => GAME_TRIGGERS.CREATE_PARCEL_WITH_ZONE()}
                        className={c(VT.toolButton.button, styles.button)}
                        tooltip={translate(
                            "PlatterMod.UI.Button.CreateParcelFromZone",
                            "Create Parcel with this Zone",
                        )}
                    />
                </VC.Section>
            </FocusDisabled>
        </div>
    );
};
