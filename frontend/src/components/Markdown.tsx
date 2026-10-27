import { Anchor, Code, Table, Text, TypographyStylesProvider } from '@mantine/core';
import ReactMarkdown, { type Components } from 'react-markdown';

/**
 * Renders untrusted Markdown (meeting notes, summaries, exported files).
 * - Raw HTML is dropped (skipHtml) — never injected into the DOM.
 * - Dangerous URLs (javascript:, data:, …) are neutralised by react-markdown's default urlTransform.
 * - Images are not loaded (tracking/privacy); they render as a link with their alt text.
 */
const components: Components = {
  a: ({ href, children }) => (
    <Anchor href={href} target="_blank" rel="noopener noreferrer nofollow">
      {children}
    </Anchor>
  ),
  img: ({ src, alt }) =>
    src ? (
      <Anchor href={src} target="_blank" rel="noopener noreferrer nofollow">
        [image: {alt || 'untitled'}]
      </Anchor>
    ) : (
      <Text span>[image: {alt}]</Text>
    ),
  code: ({ children }) => <Code>{children}</Code>,
  table: ({ children }) => (
    <Table withTableBorder withColumnBorders>
      {children}
    </Table>
  ),
};

export function Markdown({ content }: { content: string | null | undefined }) {
  if (!content) return null;
  return (
    <TypographyStylesProvider>
      <div className="gpi-markdown">
        <ReactMarkdown skipHtml components={components}>
          {content}
        </ReactMarkdown>
      </div>
    </TypographyStylesProvider>
  );
}
