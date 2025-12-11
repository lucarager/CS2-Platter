/* eslint-disable prettier/prettier */
/* eslint-disable react/no-unknown-property */
import React, { useCallback } from "react";
import { Button, Panel } from "cs2/ui";
import styles from "./styles.module.scss";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
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
                            <ChangelogItem
                            date="2025-12-10"
                            title="Platter 1.3.0 - The Snapping & Performance Update"
                            image="coui://platter/changelog/2.jpg"
                            text={
`This update introduces an all-new __Snapping System__!
Select multiple snap options simultaneously, including snapping to __parcel sides__ and __corners__, all with fresh icons.

Performance and stability have been greatly improved. The __road connection system__ and __parcel overlays__ were heavily refactored and optimized.
Also, a new changelog UI has been added to keep up with Platter's updates as they release.`}/>
                            <ChangelogItem
                            date="2025-12-8"
                            title="Platter 1.2.4 - Reworked Pre-Zone Panel"
                            image="coui://platter/changelog/1.jpg"
                            text={
`The __Pre-Zone Panel__ has been completely reworked for better stability and compatibility with mods like __Zone Reorganizer__.
A new __Base Game__ filter helps find vanilla zones instantly.
Under the hood, performance for __parcel updates__ and the __overlay__ has been significantly boosted, making gameplay smoother than ever.`}/>
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
}> = ({ title, date, text, image }) => {
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
            {image && (
            <div className={styles.card}>
                <div className={c(styles.card__inner, styles.card__inner__cl)}>
                    <div className={styles.card__inner__image}>
                        <img src={image} className={styles.card__image} />
                    </div>
                </div>
            </div>)}
            <div className={styles.card}>
                <div className={styles.card__inner}>
                    <div className={styles.card__text_container}>
                        <p className={styles.card__text} cohinline="true">
                            <HighlightedText
                                text={text}
                            />
                        </p>
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
