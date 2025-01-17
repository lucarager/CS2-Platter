import { getModule } from "cs2/modding";
import { Theme, FocusKey, UniqueFocusKey } from "cs2/bindings";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { VanillaComponentResolver } from "utils/VanillaComponentResolver";
import mod from "mod.json";
import { triggers } from '../../modBindings';
import { $bindings } from 'modBindings';

interface InfoSectionComponent {
	group: string;
	tooltipKeys: Array<string>;
	tooltipTags: Array<string>;
}

const uilStandard =                          "coui://uil/Standard/";
const uilColored =                           "coui://uil/Colored/";
const heightLockSrc = uilStandard + "ArrowsHeightLocked.svg";

const InfoSectionTheme: Theme | any = getModule(
	"game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.module.scss",
	"classes"
);

const InfoRowTheme: Theme | any = getModule(
	"game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
	"classes"
)

const InfoSection: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
)

const InfoRow: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow"
)

function handleClick(eventName : string) {
    // This triggers an event on C# side and C# designates the method to implement.
    trigger(mod.id, eventName);
}

const FocusDisabled$: FocusKey = getModule(
	"game-ui/common/focus/focus-key.ts",
	"FOCUS_DISABLED"
);

const descriptionToolTipStyle = getModule("game-ui/common/tooltip/description-tooltip/description-tooltip.module.scss", "classes");

// This is working, but it's possible a better solution is possible.
function descriptionTooltip(tooltipTitle: string | null, tooltipDescription: string | null) : JSX.Element {
    return (
        <>
            <div className={descriptionToolTipStyle.title}>{tooltipTitle}</div>
            <div className={descriptionToolTipStyle.content}>{tooltipDescription}</div>
        </>
    );
}

export const SelectedInfoPanelComponent = (componentList: any): any => {
    componentList["Platter.Systems.SelectedInfoPanelSystem"] = (e: InfoSectionComponent) => {
        const allowSpawningToggle : boolean = $bindings.infoSectionAllowSpawningToggle.use();

        return 	<InfoSection focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED} disableFocus={true} className={InfoSectionTheme.infoSection}>
                        <InfoRow
                            left={"Title"}
                            right=
                            {
                                <VanillaComponentResolver.instance.ToolButton
                                    src={heightLockSrc}
                                    selected = {allowSpawningToggle}
                                    multiSelect = {false}
                                    disabled = {false}
                                    tooltip = {"TOOLTIP"}
                                    className = {VanillaComponentResolver.instance.toolButtonTheme.button}
                                    // onSelect={() => triggers.allowSpawningToggle()}
                                />
                            }
                            tooltip={"TOOLTIP"}
                            uppercase={true}
                            disableFocus={true}
                            subRow={false}
                            className={InfoRowTheme.infoRow}
                        ></InfoRow>
                </InfoSection>
				;
    }

	return componentList as any;
}
