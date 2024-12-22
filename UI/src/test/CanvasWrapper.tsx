import React, { forwardRef, memo, MouseEventHandler, useImperativeHandle } from "react";
import useCanvas from "./useCanvas";
import { DrawFunc } from "./types";
import { HEIGHT, WIDTH } from "./BezierController";

type CanvasProps = {
    draw: DrawFunc;
    onClick?: MouseEventHandler<HTMLCanvasElement>;
    onMouseMove?: MouseEventHandler<HTMLCanvasElement>;
    onMouseDown?: MouseEventHandler<HTMLCanvasElement>;
    onMouseUp?: MouseEventHandler<HTMLCanvasElement>;
};

const Canvas = forwardRef<{ canvas(): HTMLCanvasElement | null }, CanvasProps>(
    ({ draw, onClick, onMouseMove, onMouseDown, onMouseUp }, ref) => {
        const canvasRef = useCanvas(draw);

        useImperativeHandle(ref, () => {
            return {
                canvas() {
                    return canvasRef.current;
                },
            };
        }, []);

        return (
            <canvas
                ref={canvasRef}
                width={WIDTH}
                height={HEIGHT}
                onClick={onClick}
                onMouseMove={onMouseMove}
                onMouseDown={onMouseDown}
                onMouseUp={onMouseUp}
            />
        );
    },
);

Canvas.displayName = "Canvas";

export default memo(Canvas);
