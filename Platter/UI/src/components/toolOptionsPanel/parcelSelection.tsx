import React, { useMemo, useState } from "react";
import { GAME_BINDINGS } from "gameBindings";
import styles from "./toolOptionsPanel.module.scss";
import { c } from "utils/classes";
import { useValueWrap } from "../../debug";
import { Button } from "cs2/ui";
import { VC, VF } from "components/vanilla/Components";
import { useLocalization } from "cs2/l10n";

export const ParcelSelection = function ParcelSelection() {
    const blockWidth = useValueWrap(GAME_BINDINGS.BLOCK_WIDTH.binding, "BlockWidth");
    const blockWidthMin = useValueWrap(GAME_BINDINGS.BLOCK_WIDTH_MIN.binding, "BlockWidthMin");
    const blockWidthMax = useValueWrap(GAME_BINDINGS.BLOCK_WIDTH_MAX.binding, "BlockWidthMax");
    const blockDepth = useValueWrap(GAME_BINDINGS.BLOCK_DEPTH.binding, "BlockDepth");
    const blockDepthMin = useValueWrap(GAME_BINDINGS.BLOCK_DEPTH_MIN.binding, "BlockDepthMin");
    const blockDepthMax = useValueWrap(GAME_BINDINGS.BLOCK_DEPTH_MAX.binding, "BlockDepthMax");
    const buildingCounts = useValueWrap(GAME_BINDINGS.BUILDING_COUNTS.binding, "BuildingCounts");
    const { translate } = useLocalization();

    const [hoveredSize, setHoveredSize] = useState<{ width: number; depth: number } | null>(null);

    console.log(buildingCounts);

    // Min/max sizes are constant after binding - compute grid options once
    // Generate grid from 1 to max to show all possible sizes
    const widthOptions = useMemo(
        () => Array.from({ length: blockWidthMax }, (_, index) => index + 1),
        [blockWidthMax],
    );

    const depthOptions = useMemo(
        () => Array.from({ length: blockDepthMax }, (_, index) => index + 1),
        [blockDepthMax],
    );

    const isHoveredOrSmaller = (width: number, depth: number) => {
        if (!hoveredSize) return false;
        return width <= hoveredSize.width && depth <= hoveredSize.depth;
    };

    const getBuildingCount = (width: number, depth: number) => {
        // Show empty string instead of 0 for better UX
        return buildingCounts[(width - 1) * blockDepthMax + (depth - 1)] || "";
    };

    return (
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.ParcelSize", "Parcel Size")}>
            <div className={styles.parcelSelection}>
                {/* Road Visualization */}
                <div className={styles.parcelSelection__roadViz}>
                    <div className={styles.parcelSelection__roadViz__median}></div>
                </div>
                {/* Parcel size grid */}
                <div
                    className={styles.parcelSelection__sizeGrid}
                    onMouseLeave={() => setHoveredSize(null)}>
                    {blockWidth >= blockWidthMin && blockDepth >= blockDepthMin && (
                        <div
                            className={styles.parcelSelection__selectionOverlay}
                            style={{
                                right: `${(blockDepthMax - blockDepth) * 25}rem`,
                                width: `${blockDepth * 25 - 1}rem`,
                                height: `${blockWidth * 25 - 1}rem`,
                            }}
                        />
                    )}
                    {widthOptions.map((width) => (
                        <div key={width} className={styles.parcelSelection__sizeGridRow}>
                            {depthOptions.map((depth) => {
                                const isBelowMinimum =
                                    width < blockWidthMin || depth < blockDepthMin;
                                const isSelected = width <= blockWidth && depth <= blockDepth;
                                const isHoverPreview = isHoveredOrSmaller(width, depth);

                                return (
                                    <Button
                                        key={`${width}x${depth}`}
                                        focusKey={VF.FOCUS_DISABLED}
                                        type="button"
                                        className={c(
                                            styles.parcelSelection__sizeButton,
                                            isSelected &&
                                                styles.parcelSelection__sizeButtonSelected,
                                            isHoverPreview &&
                                                styles.parcelSelection__sizeButtonHovered,
                                        )}
                                        onMouseEnter={() =>
                                            !isBelowMinimum && setHoveredSize({ width, depth })
                                        }
                                        onSelect={() => {
                                            if (!isBelowMinimum) {
                                                console.log("Selected size:", width, depth);
                                                GAME_BINDINGS.BLOCK_WIDTH.set(width);
                                                GAME_BINDINGS.BLOCK_DEPTH.set(depth);
                                            }
                                        }}>
                                        {!isBelowMinimum && getBuildingCount(width, depth)}
                                    </Button>
                                );
                            })}
                        </div>
                    ))}
                </div>
            </div>
        </VC.Section>
    );
};
