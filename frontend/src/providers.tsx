import { createTheme, MantineProvider } from '@mantine/core';
import { DatesProvider } from '@mantine/dates';
import { ModalsProvider } from '@mantine/modals';
import { Notifications } from '@mantine/notifications';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
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

export function AppProviders({ children, queryClient }: { children: ReactNode; queryClient: QueryClient }) {
  return (
    <QueryClientProvider client={queryClient}>
      <MantineProvider theme={theme} defaultColorScheme="auto">
        <DatesProvider settings={{ firstDayOfWeek: 1 }}>
          <ModalsProvider labels={{ confirm: 'Confirm', cancel: 'Cancel' }}>
            <Notifications position="top-right" />
            {children}
          </ModalsProvider>
        </DatesProvider>
      </MantineProvider>
    </QueryClientProvider>
  );
}
