import React, { MouseEventHandler, useCallback, useEffect, useState } from "react";
import Canvas from "./CanvasWrapper";
import { DrawFunc, XY } from "./types";

export const WIDTH = 500;
export const HEIGHT = 200;
const CIRCLESIZE = 10;

export const BezierController = () => {
    // Nodes
    const [startNodePos, setStartNodePos] = useState<XY>({ x: 50, y: 100 });
    const [startHandlePos, setStartHandlePos] = useState<XY>({
        x: 100,
        y: 100,
    });
    const [endHandlePos, setEndHandlePos] = useState<XY>({ x: 200, y: 50 });
    const [endNodePos, setEndNodePos] = useState<XY>({
        x: 300,
        y: 50,
    });
    const [movingNode, setMovingNode] = useState<string>("none");

    // Mouse tracker
    const [firstPos, setFirstPos] = useState<XY>({ x: 0, y: 0 });
    const [curPos, setCurPos] = useState<XY>({ x: 0, y: 0 });
    const [isDown, setIsDown] = useState(false);

    // Canvas
    const canvasRef = React.createRef<{ canvas(): HTMLCanvasElement | null }>();

    const getXY = useCallback(
        (x: number, y: number) => {
            const canvas = canvasRef.current?.canvas();
            const rect = canvas?.getBoundingClientRect();
            return {
                x: x - (rect?.left ?? 0),
                y: y - (rect?.top ?? 0),
            };
        },
        [canvasRef],
    );

    function drawCircle(
        context: CanvasRenderingContext2D,
        pos: {
            x: number;
            y: number;
        },
    ) {
        context.fillStyle = "#4bc3f1";
        context.beginPath();
        context.arc(pos.x, pos.y, CIRCLESIZE / 2, 0, 2 * Math.PI);
        context.fill();
    }

    function drawLine(
        context: CanvasRenderingContext2D,
        from: {
            x: number;
            y: number;
        },
        to: {
            x: number;
            y: number;
        },
    ) {
        context.strokeStyle = "#4bc3f166";
        context.lineWidth = 3;
        context.beginPath();
        context.moveTo(from.x, from.y);
        context.lineTo(to.x, to.y);
        context.stroke();
    }

    const draw: DrawFunc = useCallback(
        (context, frameCount) => {
            context.clearRect(0, 0, WIDTH, HEIGHT);

            context.fillStyle = "transparent";
            context.strokeStyle = "#F0FFFF";
            context.lineWidth = 2;
            context.beginPath();
            context.moveTo(startNodePos.x, startNodePos.y);
            context.bezierCurveTo(
                startHandlePos.x,
                startHandlePos.y,
                endHandlePos.x,
                endHandlePos.y,
                endNodePos.x,
                endNodePos.y,
            );
            context.stroke();
            context.fill();

            drawLine(context, startNodePos, startHandlePos);
            drawLine(context, endNodePos, endHandlePos);
            drawCircle(context, startNodePos);
            drawCircle(context, startHandlePos);
            drawCircle(context, endNodePos);
            drawCircle(context, endHandlePos);
        },
        [startNodePos, startHandlePos, endHandlePos, endNodePos],
    );

    const collision = useCallback((a: XY, b: XY) => {
        return (
            a.x >= b.x - CIRCLESIZE &&
            a.x <= b.x + CIRCLESIZE &&
            a.y >= b.y - CIRCLESIZE &&
            a.y <= b.y + CIRCLESIZE
        );
    }, []);

    useEffect(() => {
        const handleMouseMove = (e: MouseEvent) => {
            if (!isDown) return; // we will only act if mouse button is down
            console.log("handleMouseMove");
            // Update position
            setCurPos(getXY(e.clientX, e.clientY));

            switch (movingNode) {
                case "startNode":
                    setStartNodePos(curPos);
                    break;
                case "startHandle":
                    setStartHandlePos(curPos);
                    break;
                case "endHandle":
                    setEndHandlePos(curPos);
                    break;
                case "endNode":
                    setEndNodePos(curPos);
                    break;
            }
        };

        const handleMouseUp = () => {
            console.log("handleMouseUp");
            setIsDown(false);
            setMovingNode("none");
        };

        window.addEventListener("mousemove", handleMouseMove);
        window.addEventListener("mouseup", handleMouseUp);

        return () => {
            window.removeEventListener("mousemove", handleMouseMove);
            window.removeEventListener("mouseup", handleMouseUp);
        };
    }, [
        collision,
        curPos,
        endHandlePos,
        endNodePos,
        firstPos,
        getXY,
        isDown,
        startHandlePos,
        startNodePos,
    ]);

    const handleMouseDown: MouseEventHandler<HTMLCanvasElement> = useCallback(
        (e) => {
            console.log("handleMouseDown");
            const pos = getXY(e.clientX, e.clientY);
            // Check if we clicked any node
            if (collision(pos, startNodePos)) {
                console.log("handleMouseDown moving start node");
                setMovingNode("startNode");
            } else if (collision(pos, startHandlePos)) {
                console.log("handleMouseDown moving start handle");
                setMovingNode("startHandle");
            } else if (collision(pos, endHandlePos)) {
                console.log("handleMouseDown moving end handle");
                setMovingNode("endHandle");
            } else if (collision(pos, endNodePos)) {
                console.log("handleMouseDown moving end node");
                setMovingNode("endNode");
            }

            setFirstPos(pos);
            setCurPos(pos);
            setIsDown(true);
        },
        [getXY],
    );

    return (
        <div>
            <Canvas draw={draw} ref={canvasRef} onMouseDown={handleMouseDown} />
        </div>
    );
};
