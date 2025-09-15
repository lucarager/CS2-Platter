import React from "react";
import { getModule } from "cs2/modding";
import { Theme, FocusKey } from "cs2/bindings";
import { trigger, useValue } from "cs2/api";
import mod from "mod.json";
import { GAME_BINDINGS } from "gameBindings";
import { VanillaComponents, VanillaThemes } from "components/vanilla/Components";

interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

const uilStandard = "coui://uil/Standard/";
const uilColored = "coui://uil/Colored/";
const heightLockSrc = uilStandard + "ArrowsHeightLocked.svg";

const InfoSectionTheme: Theme | any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.module.scss",
    "classes",
);

const InfoRowTheme: Theme | any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
    "classes",
);

const InfoSection: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection",
);

const InfoRow: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow",
);

function handleClick(eventName: string) {
    // This triggers an event on C# side and C# designates the method to implement.
    trigger(mod.id, eventName);
}

const FocusDisabled$ = getModule<FocusKey>("game-ui/common/focus/focus-key.ts", "FOCUS_DISABLED");

const descriptionToolTipStyle = getModule<any>(
    "game-ui/common/tooltip/description-tooltip/description-tooltip.module.scss",
    "classes",
);

// This is working, but it's possible a better solution is possible.
function descriptionTooltip(
    tooltipTitle: string | null,
    tooltipDescription: string | null,
): JSX.Element {
    return (
        <>
            <div className={descriptionToolTipStyle.title}>{tooltipTitle}</div>
            <div className={descriptionToolTipStyle.content}>{tooltipDescription}</div>
        </>
    );
}

export const SelectedInfoPanelComponent = (componentList: any) => {
    componentList["Platter.Systems.SelectedInfoPanelSystem"] = (e: InfoSectionComponent) => {
        // eslint-disable-next-line react-hooks/rules-of-hooks
        const allowSpawningToggle: boolean = useValue(
            GAME_BINDINGS.ALLOW_SPAWNING_INFO_SECTION.binding,
        );

        return (
            <InfoSection
                focusKey={FocusDisabled$}
                disableFocus={true}
                className={InfoSection.infoSection}>
                <InfoRow
                    left={"Title"}
                    right={
                        <VanillaComponents.ToolButton
                            src={heightLockSrc}
                            selected={allowSpawningToggle}
                            multiSelect={false}
                            disabled={false}
                            tooltip={"TOOLTIP"}
                            className={VanillaThemes.toolButton.button}
                            // onSelect={() => triggers.allowSpawningToggle()}
                        />
                    }
                    tooltip={"TOOLTIP"}
                    uppercase={true}
                    disableFocus={true}
                    subRow={false}
                    className={InfoRow.infoRow}></InfoRow>
            </InfoSection>
        );
    };

    return componentList as any;
};
