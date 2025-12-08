/* eslint-disable prettier/prettier */
/* eslint-disable react/no-unknown-property */
import React, { useCallback } from "react";
import { Button, Panel } from "cs2/ui";
import styles from "./styles.module.scss";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import { useLocalization } from "cs2/l10n";
import { useValue } from "cs2/api";

export type BlockControlProps = Record<string, never>;

export const ChangelogModal = () => {
    const modalBinding = useValue(GAME_BINDINGS.MODAL__CHANGELOG.binding);

    const { translate } = useLocalization();

    const handleCloseOnSelect = useCallback(() => {
        GAME_BINDINGS.MODAL__CHANGELOG.set(false);
    }, []);

    if (!modalBinding) {
        return <></>;
    }

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
                                <h2>
                                    {translate(
                                        "PlatterMod.UI.Modals.Changelog.Title",
                                        "What's New",
                                    )}
                                </h2>
                            </div>
                        </span>
                        <Button
                            variant="icon"
                            src="Media/Glyphs/Close.svg"
                            onSelect={handleCloseOnSelect}
                        />
                    </div>
                }>
                <VC.Scrollable>
                    <div className={styles.changelog}>
                        <div className={styles.changelog__left}></div>
                        <div className={styles.changelog__right}>
                            <div className={styles.versionDivider}>
                                <div className={styles.versionDivider__inner}>
                                    <div className={styles.versionDivider__circle}></div>
                                    <div className={styles.versionDivider__time}>
                                        5 days ago
                                    </div>
                                    <h3>Platter 1.2.0</h3>
                                </div>
                            </div>
                            <div className={styles.card}>
                                <div className={styles.card__inner}>
                                    <div className={styles.card__inner__image}>
                                        <img
                                            src="coui://platter/tu1.png"
                                            className={styles.card__image}
                                        />
                                    </div>
                                </div>
                            </div>
                            <div className={styles.card}>
                                <div className={styles.card__inner}>
                                    <div className={styles.card__text_container}>
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
                                                        <div className={styles.versionDivider}>
                                <div className={styles.versionDivider__inner}>
                                    <div className={styles.versionDivider__circle}></div>
                                    <div className={styles.versionDivider__time}>
                                        5 days ago
                                    </div>
                                    <h3>Platter 1.1.0</h3>
                                </div>
                            </div>
                            <div className={styles.card}>
                                <div className={styles.card__inner}>
                                    <div className={styles.card__text_container}>
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
                                                                                    <div className={styles.versionDivider}>
                                <div className={styles.versionDivider__inner}>
                                    <div className={styles.versionDivider__circle}></div>
                                    <div className={styles.versionDivider__time}>
                                        5 days ago
                                    </div>
                                    <h3>Platter 1.1.0</h3>
                                </div>
                            </div>
                            <div className={styles.card}>
                                <div className={styles.card__inner}>
                                    <div className={styles.card__text_container}>
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
                        </div>
                    </div>
                </VC.Scrollable>
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
