/* eslint-disable prettier/prettier */
/* eslint-disable react/no-unknown-property */
import React, { useCallback, useState } from "react";
import { Button, Panel } from "cs2/ui";
import styles from "./styles.module.scss";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";

export type BlockControlProps = Record<string, never>;

export const WelcomeModal = () => {
    const modalBinding = useValue(GAME_BINDINGS.MODAL__FIRST_LAUNCH.binding);
    const [activePage, setActivePage] = useState(0);
    const { translate } = useLocalization();

    const handleCloseOnSelect = useCallback(() => {
        GAME_TRIGGERS.MODAL_DISMISS("first_launch");
    }, []);

    // if (modalBinding) {
    //     return <></>;
    // }

    return (
        <div className={styles.wrapper}>
            <Panel
                className={styles.panel}
                theme={{
                    ...VT.panel,
                    header: styles.header,
                }}
                header={
                    <div className={styles.header__inner}>
                        <span className={styles.headerText}>
                            <div className={styles.intro}>
                                <h2>{translate("PlatterMod.UI.Modals.FirstLaunch.Title")}</h2>
                                <h3>{translate("PlatterMod.UI.Modals.FirstLaunch.Subtitle")}</h3>
                            </div>
                        </span>
                        <Button
                            variant="icon"
                            src="Media/Glyphs/Close.svg"
                            onSelect={handleCloseOnSelect}
                        />
                    </div>
                }>
                <VC.PageSwitcher
                    className={styles.pageSwitcher}
                    transitionStyles={VT.horizontalTransition}
                    activePage={activePage}>
                    {activePage != 0 ? null : (
                        <VC.Page className={VT.whatsNewPage["whats-new-page"]} key={0}>
                            <VC.Scrollable>
                                <div className={styles.card}>
                                    <div className={styles.card__inner}>
                                        <div className={styles.card__inner__image}>
                                            <img
                                                src="coui://platter/tu1.png"
                                                className={styles.card__image}
                                            />
                                        </div>
                                        <div className={styles.card__text_container}>
                                            <h3>
                                                {translate(
                                                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial1.Title",
                                                )}
                                            </h3>
                                            <p className={styles.card__text} cohinline="true">
                                                <HighlightedText
                                                    text={translate(
                                                        "PlatterMod.UI.Modals.FirstLaunch.Tutorial1.Text",
                                                    )}
                                                />
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            </VC.Scrollable>
                        </VC.Page>
                    )}
                    {activePage != 1 ? null : (
                        <VC.Page className={VT.whatsNewPage["whats-new-page"]} key={1}>
                            <VC.Scrollable>
                                <div className={styles.card}>
                                    <div className={styles.card__inner}>
                                        <div className={styles.card__inner__image}>
                                            <img
                                                src="coui://platter/tu3.png"
                                                className={styles.card__image}
                                            />
                                        </div>
                                        <div className={styles.card__text_container}>
                                            <h3>
                                                {translate(
                                                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial2.Title",
                                                )}
                                            </h3>
                                            <p className={styles.card__text} cohinline="true">
                                                <HighlightedText
                                                    text={translate(
                                                        "PlatterMod.UI.Modals.FirstLaunch.Tutorial2.Text",
                                                    )}
                                                />
                                            </p>
                                        </div>
                                    </div>
                                    <div className={styles.card__inner}>
                                        <div className={styles.card__inner__image}>
                                            <img
                                                src="coui://platter/tu3.png"
                                                className={styles.card__image}
                                            />
                                        </div>
                                        <div className={styles.card__text_container}>
                                            <h3>
                                                {translate(
                                                    "PlatterMod.UI.Modals.FirstLaunch.Tutorial2-2.Title",
                                                )}
                                            </h3>
                                            <p className={styles.card__text} cohinline="true">
                                                <HighlightedText
                                                    text={translate(
                                                        "PlatterMod.UI.Modals.FirstLaunch.Tutorial2-2.Text",
                                                    )}
                                                />
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            </VC.Scrollable>
                        </VC.Page>
                    )}
                    {activePage != 2 ? null : (
                        <VC.Page className={VT.whatsNewPage["whats-new-page"]} key={2}>
                            <VC.Scrollable>
                                <div className={styles.card}>
                                    <div className={styles.card__inner}>
                                        <div className={styles.card__inner__image}>
                                            <img
                                                src="coui://platter/tu3.png"
                                                className={styles.card__image}
                                            />
                                        </div>
                                        <div className={styles.card__text_container}>
                                            <p className={styles.card__text} cohinline="true">
                                                <HighlightedText
                                                    text={translate(
                                                        "PlatterMod.UI.Modals.FirstLaunch.Tutorial3.Text",
                                                    )}
                                                />
                                            </p>
                                        </div>
                                    </div>
                                    <div className={styles.card__inner}>
                                        <div className={styles.card__inner__image}>
                                            <img
                                                src="coui://platter/tu4.png"
                                                className={styles.card__image}
                                            />
                                        </div>
                                        <div className={styles.card__text_container}>
                                            <p className={styles.card__text} cohinline="true">
                                                <HighlightedText
                                                    text={translate(
                                                        "PlatterMod.UI.Modals.FirstLaunch.Tutorial4.Text",
                                                    )}
                                                />
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            </VC.Scrollable>
                        </VC.Page>
                    )}
                    {activePage != 3 ? null : (
                        <VC.Page className={VT.whatsNewPage["whats-new-page"]} key={3}>
                            <VC.Scrollable>
                                <div className={styles.card}>
                                    <div className={styles.card__inner}>
                                        {/* <img
                                        src="coui://platter/placeholder.png"
                                        className={styles.card__image}
                                    /> */}
                                        <div className={styles.card__text_container}>
                                            <h3>
                                                {translate(
                                                    "PlatterMod.UI.Modals.FirstLaunch.Disclaimer.Title",
                                                )}
                                            </h3>
                                            <p className={styles.alert}>
                                                {translate(
                                                    "PlatterMod.UI.Modals.FirstLaunch.Disclaimer.Text",
                                                )}
                                            </p>
                                            <div className={styles.action}>
                                                <Button
                                                    variant="primary"
                                                    onSelect={handleCloseOnSelect}>
                                                    {translate(
                                                        "PlatterMod.UI.Modals.FirstLaunch.Button",
                                                    )}
                                                </Button>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </VC.Scrollable>
                        </VC.Page>
                    )}
                </VC.PageSwitcher>
                <VC.PageSelector
                    pages={4}
                    selected={activePage}
                    onSelect={(i) => {
                        setActivePage(i);
                    }}
                />
            </Panel>
        </div>
    );
};

interface HighlightedTextProps {
    text: string | null;
    highlightClassName?: string; // optional custom class for highlight
}

function parseHighlightedText(input: string) {
    const parts: { text: string; highlight: boolean }[] = [];

    // Split on __ while keeping the delimited text
    const split = input.split(/(__.*?__)/);

    for (const part of split) {
        if (part.startsWith("__") && part.endsWith("__")) {
            // Remove the __ markers
            parts.push({
                text: part.slice(2, -2),
                highlight: true,
            });
        } else if (part.length > 0) {
            parts.push({
                text: part,
                highlight: false,
            });
        }
    }

    return parts;
}

export const HighlightedText: React.FC<HighlightedTextProps> = ({ text }) => {
    if (text == null) return <></>;

    const parts = parseHighlightedText(text);

    return (
        <>
            {parts.map((part, i) =>
                part.highlight ? (
                    <span key={i} className={styles.highlight}>
                        {part.text}
                    </span>
                ) : (
                    <span key={i}>{part.text}</span>
                ),
            )}
        </>
    );
};
