/* eslint-disable prettier/prettier */
/* eslint-disable react/no-unknown-property */
import React, { useCallback } from "react";
import { Button, Panel } from "cs2/ui";
import styles from "./styles.module.scss";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useLocalization } from "cs2/l10n";
import { useValue } from "cs2/api";
import { c } from "utils/classes";

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
                className={c(styles.panel, styles.changelogPanel)}
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
                            {/* March 19th */}
                            <ChangelogItem
                                date="2026-03-19"
                                title="Platter 1.5 - Big Spring Update: 1-wide Parcels, Snapping, Stability, and more"
                                image="coui://platter/changelog/4.jpg"
                                linkKey="CHANGELOG_150"
                                linkLabel="Full Changelog"
                                alertKey="CHANGELOG_150"
                                alertLink="Open CS:2 Modding Discord"
                                alertText={`A lot changed in this release. Being a solo dev, unexpected bugs can still slip through (even with extensive testing). If you run into one, you can let me know on Discord via the link below!`}
                                text={`
Introducing __Narrow (1-wide)__ and __Extra-Wide (7-8)__ parcels, plus a fully reworked __snapping system__, and a number of fixes and improvements to stability and performance.

- ___Snapping___ is improved with __line tool start/end point snapping__, cleaner multi-snap behavior, and better snap feedback.
- ___Stability___ was improved with fixes for __road drawing related game crashes__ and __parcel cells being "eaten" by vanilla cells__, plus a number of fixes for parcel-road interactions, zone index desync, and picker/relocate behavior.
- ___Performance___ is substantially improved with faster UI data delivery, smarter prefab caching, and optimized parcel updates.
- ___Parcel Overlays___ have been reworked significantly. Road connection icons have also been refined. `}
                            />
                            {/* December 12th */}
                            <ChangelogItem
                                date="2025-12-12"
                                title="Platter 1.3.1 - Snap improvements & new auto-overlay Setting"
                                text={`Snap to Parcel Sides now allows snapping parcels together in any orientation, making creating complex layouts easier than ever! Other snapping options have been tweaked to improve usability.

Added a new setting to the Settings Page that allows disabling the "auto-overlay" for vanilla tools. For those that want to fully control when those overlays pop up!`}
                            />
                            {/* December 10th */}
                            <ChangelogItem
                                date="2025-12-10"
                                title="Platter 1.3.0 - The Snapping & Performance Update"
                                image="coui://platter/changelog/2.jpg"
                                text={`This update introduces an all-new __Snapping System__!
Select multiple snap options simultaneously, including snapping to __parcel sides__ and __corners__, all with fresh icons.

Performance and stability have been greatly improved. The __road connection system__ and __parcel overlays__ were heavily refactored and optimized.
Also, a new changelog UI has been added to keep up with Platter's updates as they release.`}
                            />
                            {/* December 8th */}
                            <ChangelogItem
                                date="2025-12-8"
                                title="Platter 1.2.4 - Reworked Pre-Zone Panel"
                                image="coui://platter/changelog/1.jpg"
                                text={`The __Pre-Zone Panel__ has been completely reworked for better stability and compatibility with mods like __Zone Reorganizer__.
A new __Base Game__ filter helps find vanilla zones instantly.
Under the hood, performance for __parcel updates__ and the __overlay__ has been significantly boosted, making gameplay smoother than ever.`}
                            />
                            {/* December 8 */}
                            <ChangelogItem
                                date="2025-12-8"
                                title="New Parcel UI & Tracking System"
                                image="coui://platter/changelog/3.jpg"
                                text={`A major polish update featuring a new __corner lot detection system__! Parcels now intelligently track both front and side road connections, warning you of missing connections before placement.
The __parcel overlay__ has been overhauled for precision, now matching the parcel's exact size and visualizing road connections on all sides.
__Building verification tooltips__ now accurately track corner buildings, and critical bugs with the relocate button have been fixed.`}
                            />
                            {/* December 7 */}
                            <ChangelogItem
                                date="2025-12-5"
                                title="Improvements to Plopping"
                                text={`Parcels will now show missing road connection before placing them. Fixed Parcels causing building props and trees to disappear. Fixed brush tool being able to delete parcels.`}
                            />
                            {/* December 5 */}
                            <ChangelogItem
                                date="2025-12-5"
                                title="Pre-Zone QOL Improvements"
                                text={`Added pack/theme filtering and searching to the Pre-Zone Panel. Adjusted size and position of the panel.`}
                            />
                            {/* December 3 */}
                            <ChangelogItem
                                date="2025-12-3"
                                title="The frontage update"
                                text={
                                    "Buildings spawning on parcels will now always face the front of the parcel."
                                }
                            />
                        </div>
                    </div>
                </VC.Scrollable>
            </Panel>
        </div>
    );
};

const ChangelogItem: React.FC<{
    title: string;
    date: string;
    text: string;
    image?: string;
    alertText?: string;
    alertLink?: string;
    alertKey?: string;
    linkKey?: string;
    linkLabel?: string;
}> = ({
    title,
    date,
    text,
    image,
    alertText,
    alertLink,
    alertKey,
    linkKey,
    linkLabel,
}) => {
    return (
        <>
            <div className={styles.versionDivider}>
                <div className={styles.versionDivider__inner}>
                    <div className={styles.versionDivider__circle}></div>
                    <div className={styles.versionDivider__time}>
                        <RelativeTime date={date} />
                    </div>
                    <h3>{title}</h3>
                </div>
            </div>
            {linkKey && (
                <div className={styles.card}>
                    <div className={c(styles.card__inner, styles.card__inner__btn)}>
                        <Button
                            variant="text"
                            className={styles.linkButton}
                            onSelect={() => GAME_TRIGGERS.OPEN_LINK(linkKey)}>
                            {linkLabel || "Read more"}
                        </Button>
                    </div>
                </div>
            )}
            {image && (
                <div className={styles.card}>
                    <div className={c(styles.card__inner, styles.card__inner__cl)}>
                        <div className={styles.card__inner__image}>
                            <img src={image} className={styles.card__image} />
                        </div>
                    </div>
                </div>
            )}
            <div className={styles.card}>
                <div className={styles.card__inner}>
                    <div className={styles.card__text_container}>
                        <p className={styles.card__text} cohinline="true">
                            <HighlightedText text={text} />
                        </p>
                        {alertText && (
                            <div className={styles.alert}>
                                <p className={styles.card__text} cohinline="true">
                                    <HighlightedText text={alertText} />
                                </p>
                                {alertKey && (
                                    <div className={styles.alertLinkWrap}>
                                        <Button
                                            variant="text"
                                            className={c(styles.linkButton, styles.alertLinkButton)}
                                            onSelect={() => GAME_TRIGGERS.OPEN_LINK(alertKey)}>
                                            {alertLink || "Open link"}
                                        </Button>
                                    </div>
                                )}
                            </div>
                        )}
                    </div>
                </div>
            </div>
        </>
    );
};

const RelativeTime: React.FC<{ date: string }> = ({ date }) => {
    const getRelativeTime = (dateStr: string) => {
        const date = new Date(dateStr);
        const now = new Date();
        const seconds = Math.floor((now.getTime() - date.getTime()) / 1000);

        const intervals = [
            { label: "year", seconds: 31536000 },
            { label: "month", seconds: 2592000 },
            { label: "day", seconds: 86400 },
            { label: "hour", seconds: 3600 },
            { label: "minute", seconds: 60 },
            { label: "second", seconds: 1 },
        ];

        for (const interval of intervals) {
            const count = Math.floor(seconds / interval.seconds);
            if (count >= 1) {
                return `${count} ${interval.label}${count !== 1 ? "s" : ""} ago`;
            }
        }
        return "just now";
    };

    return <>{getRelativeTime(date)}</>;
};

interface HighlightedTextProps {
    text: string | null;
    highlightClassName?: string; // optional custom class for highlight
}

function parseHighlightedText(input: string) {
    const parts: { text: string; highlight: boolean; bold: boolean }[] = [];

    // Split on ___ and __ while keeping the delimited text
    const split = input.split(/(___.*?___|__.*?__)/);

    for (const part of split) {
        if (part.startsWith("___") && part.endsWith("___")) {
            // Remove the ___ markers
            parts.push({
                text: part.slice(3, -3),
                highlight: false,
                bold: true,
            });
        } else if (part.startsWith("__") && part.endsWith("__")) {
            // Remove the __ markers
            parts.push({
                text: part.slice(2, -2),
                highlight: true,
                bold: false,
            });
        } else if (part.length > 0) {
            parts.push({
                text: part,
                highlight: false,
                bold: false,
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
                part.bold ? (
                    <strong key={i}>{part.text}</strong>
                ) : part.highlight ? (
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
