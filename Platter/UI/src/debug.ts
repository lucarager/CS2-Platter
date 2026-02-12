import { useEffect, useRef } from "react";
import { useValue, ValueBinding } from "cs2/api";

// Toggle this to enable/disable logging globally
// Changed to a function to allow runtime toggling via console: window._CS2_DEBUG = false
const isDebugEnabled = () => (window as any)._CS2_DEBUG ?? true;

// Enable by default
(window as any)._CS2_DEBUG = false;

/**
 * Wraps useValue with optional debug logging.
 * @param binding The game binding to subscribe to.
 * @param debugName If provided, logs changes to console when DEBUG_ENABLED is true.
 */
export function useValueWrap<T>(binding: ValueBinding<T>, debugName?: string): T {
    const value = useValue(binding);

    // We use refs to track history without triggering re-renders
    const prevValue = useRef(value);
    const renderCount = useRef(0);

    useEffect(() => {
        // Only log if enabled, name is provided, and value actually changed
        // Simple equality check might not be enough for deep objects, but sufficient for primitives/references
        if (isDebugEnabled() && debugName && prevValue.current !== value) {
            console.log(`[Binding Update] ${debugName}:`, {
                prev: prevValue.current,
                next: value,
                renderCount: renderCount.current,
            });
        }
        prevValue.current = value;
    }, [value, debugName]);

    renderCount.current++;
    return value;
}

/**
 * Debug hook to log which props caused a component to re-render.
 */
export function useTraceProps(props: any, componentName: string) {
    const prev = useRef(props);

    useEffect(() => {
        if (!isDebugEnabled()) return;

        const changedProps = Object.entries(props).reduce((ps, [k, v]) => {
            if (prev.current[k] !== v) {
                ps[k] = [prev.current[k], v];
            }
            return ps;
        }, {} as any);

        if (Object.keys(changedProps).length > 0) {
            console.log(`[${componentName}] Changed props:`, changedProps);
        }
        prev.current = props;
    });
}

export function useRenderTracker(componentName: string) {
    useEffect(() => {
        if (!isDebugEnabled()) return;

        console.warn(`[${componentName}] rendered`);
    });
}
