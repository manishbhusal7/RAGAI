/**
 * Markdown parsing utilities
 * Extracts and processes markdown content from text
 */

/**
 * Regex patterns for markdown parsing
 */
export const MARKDOWN_PATTERNS = {
  /** Pattern for code blocks with language specification */
  CODE_BLOCK: /```(\w+)?\n?([\s\S]*?)```/g,

  /** Pattern for inline code */
  INLINE_CODE: /`([^`]+)`/g,

  /** Pattern for headers */
  HEADERS: /^#{1,6}\s+(.+)$/gm,

  /** Pattern for bold text */
  BOLD: /\*\*(.*?)\*\*/g,

  /** Pattern for italic text */
  ITALIC: /\*(.*?)\*/g,

  /** Pattern for lists */
  LIST_ITEM: /^[-*+]\s+(.+)$/gm,

  /** Pattern for numbered lists */
  NUMBERED_LIST: /^\d+\.\s+(.+)$/gm,
} as const;

/**
 * Config for code block extraction
 */
export const CODE_BLOCK_EXTRACTION_CONFIG = {
  /** Default language if none specified */
  defaultLanguage: 'text' as const,

  /** Maximum code blocks to extract from text */
  maxBlocks: 20,

  /** Whether to trim whitespace from code content */
  trimContent: true,
} as const;

/**
 * Extract all code blocks from text
 * @param text - The text to parse
 * @returns Array of code blocks with language and content
 */
export const extractCodeBlocks = (
  text: string
): Array<{ language: string; content: string }> => {
  const blocks: Array<{ language: string; content: string }> = [];
  let match;
  let count = 0;

  const regex = new RegExp(MARKDOWN_PATTERNS.CODE_BLOCK.source, 'g');

  while ((match = regex.exec(text)) !== null && count < CODE_BLOCK_EXTRACTION_CONFIG.maxBlocks) {
    const language = match[1] || CODE_BLOCK_EXTRACTION_CONFIG.defaultLanguage;
    const content = CODE_BLOCK_EXTRACTION_CONFIG.trimContent ? match[2].trim() : match[2];

    blocks.push({ language, content });
    count++;
  }

  return blocks;
};

/**
 * Extract inline code snippets from text
 * @param text - The text to parse
 * @returns Array of inline code snippets
 */
export const extractInlineCode = (text: string): string[] => {
  const codes: string[] = [];
  let match;

  const regex = new RegExp(MARKDOWN_PATTERNS.INLINE_CODE.source, 'g');

  while ((match = regex.exec(text)) !== null) {
    codes.push(match[1]);
  }

  return codes;
};

/**
 * Check if text contains code blocks
 * @param text - The text to check
 * @returns true if text contains code blocks
 */
export const hasCodeBlocks = (text: string): boolean => {
  return MARKDOWN_PATTERNS.CODE_BLOCK.test(text);
};

/**
 * Check if text contains inline code
 * @param text - The text to check
 * @returns true if text contains inline code
 */
export const hasInlineCode = (text: string): boolean => {
  return MARKDOWN_PATTERNS.INLINE_CODE.test(text);
};

/**
 * Remove all code blocks from text
 * @param text - The text to process
 * @returns Text with code blocks removed
 */
export const removeCodeBlocks = (text: string): string => {
  return text.replace(MARKDOWN_PATTERNS.CODE_BLOCK, '').trim();
};

/**
 * Get plain text without markdown formatting
 * @param text - The formatted text
 * @returns Plain text without markdown
 */
export const stripMarkdown = (text: string): string => {
  return text
    .replace(MARKDOWN_PATTERNS.CODE_BLOCK, '')
    .replace(MARKDOWN_PATTERNS.INLINE_CODE, '$1')
    .replace(MARKDOWN_PATTERNS.BOLD, '$1')
    .replace(MARKDOWN_PATTERNS.ITALIC, '$1')
    .replace(MARKDOWN_PATTERNS.HEADERS, '$1')
    .trim();
};
