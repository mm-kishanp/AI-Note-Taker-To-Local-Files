import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Anchor, Code, Table, Text, TypographyStylesProvider } from '@mantine/core';
import ReactMarkdown from 'react-markdown';
/**
 * Renders untrusted Markdown (meeting notes, summaries, exported files).
 * - Raw HTML is dropped (skipHtml) — never injected into the DOM.
 * - Dangerous URLs (javascript:, data:, …) are neutralised by react-markdown's default urlTransform.
 * - Images are not loaded (tracking/privacy); they render as a link with their alt text.
 */
const components = {
    a: ({ href, children }) => (_jsx(Anchor, { href: href, target: "_blank", rel: "noopener noreferrer nofollow", children: children })),
    img: ({ src, alt }) => src ? (_jsxs(Anchor, { href: src, target: "_blank", rel: "noopener noreferrer nofollow", children: ["[image: ", alt || 'untitled', "]"] })) : (_jsxs(Text, { span: true, children: ["[image: ", alt, "]"] })),
    code: ({ children }) => _jsx(Code, { children: children }),
    table: ({ children }) => (_jsx(Table, { withTableBorder: true, withColumnBorders: true, children: children })),
};
export function Markdown({ content }) {
    if (!content)
        return null;
    return (_jsx(TypographyStylesProvider, { children: _jsx("div", { className: "gpi-markdown", children: _jsx(ReactMarkdown, { skipHtml: true, components: components, children: content }) }) }));
}
