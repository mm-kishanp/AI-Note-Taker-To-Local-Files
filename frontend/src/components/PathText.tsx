import { ActionIcon, CopyButton, Group, Text, Tooltip, type TextProps } from '@mantine/core';
import { IconCheck, IconCopy } from '@tabler/icons-react';

export interface PathTextProps extends TextProps {
  path: string | null | undefined;
  /** Hide the copy button. */
  noCopy?: boolean;
  placeholder?: string;
}

/** Monospace, wrap-anywhere path with a copy-to-clipboard button. */
export function PathText({ path, noCopy, placeholder = '—', size = 'sm', ...rest }: PathTextProps) {
  if (!path) {
    return (
      <Text size={size} c="dimmed" {...rest}>
        {placeholder}
      </Text>
    );
  }
  return (
    <Group gap={4} wrap="nowrap" align="flex-start" style={{ minWidth: 0 }}>
      <Text ff="monospace" size={size} style={{ wordBreak: 'break-all', minWidth: 0 }} title={path} {...rest}>
        {path}
      </Text>
      {!noCopy && (
        <CopyButton value={path} timeout={1500}>
          {({ copied, copy }) => (
            <Tooltip label={copied ? 'Copied' : 'Copy path'} withArrow>
              <ActionIcon
                variant="subtle"
                color={copied ? 'teal' : 'gray'}
                size="sm"
                onClick={copy}
                aria-label={copied ? 'Path copied' : `Copy path ${path}`}
              >
                {copied ? <IconCheck size={14} /> : <IconCopy size={14} />}
              </ActionIcon>
            </Tooltip>
          )}
        </CopyButton>
      )}
    </Group>
  );
}
