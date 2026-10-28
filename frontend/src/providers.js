import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { createTheme, MantineProvider } from '@mantine/core';
import { DatesProvider } from '@mantine/dates';
import { ModalsProvider } from '@mantine/modals';
import { Notifications } from '@mantine/notifications';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ApiError } from './services/api';
export const theme = createTheme({
    primaryColor: 'green',
    defaultRadius: 'sm',
    fontFamilyMonospace: 'ui-monospace, SFMono-Regular, Consolas, "Liberation Mono", monospace',
});
export function createQueryClient() {
    return new QueryClient({
        defaultOptions: {
            queries: {
                staleTime: 15_000,
                refetchOnWindowFocus: true,
                // Client errors (4xx) are not transient — don't hammer the server.
                retry: (count, err) => !(err instanceof ApiError && err.status >= 400 && err.status < 500) && count < 2,
            },
            mutations: { retry: false },
        },
    });
}
export function AppProviders({ children, queryClient }) {
    return (_jsx(QueryClientProvider, { client: queryClient, children: _jsx(MantineProvider, { theme: theme, defaultColorScheme: "auto", children: _jsx(DatesProvider, { settings: { firstDayOfWeek: 1 }, children: _jsxs(ModalsProvider, { labels: { confirm: 'Confirm', cancel: 'Cancel' }, children: [_jsx(Notifications, { position: "top-right" }), children] }) }) }) }));
}
