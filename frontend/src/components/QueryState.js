import { jsx as _jsx, Fragment as _Fragment } from "react/jsx-runtime";
import { Skeleton, Stack } from '@mantine/core';
import { ProblemAlert } from './ProblemAlert';
export function LoadingRows({ rows = 5, height = 28 }) {
    return (_jsx(Stack, { gap: "xs", "aria-busy": "true", "aria-label": "Loading", children: Array.from({ length: rows }).map((_, i) => (_jsx(Skeleton, { height: height, radius: "sm" }, i))) }));
}
/** Loading skeleton / error alert / content switch for a query. */
export function QueryState({ isLoading, error, onRetry, skeleton, children }) {
    if (isLoading)
        return _jsx(_Fragment, { children: skeleton ?? _jsx(LoadingRows, {}) });
    if (error)
        return _jsx(ProblemAlert, { error: error, onRetry: onRetry });
    return _jsx(_Fragment, { children: children });
}
