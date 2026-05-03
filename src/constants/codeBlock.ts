/**
 * Icon components and SVG definitions
 * Centralized icon management for UI components
 */

import React from 'react';

/**
 * SVG icon configuration interface
 */
interface SVGIconProps {
  width: number;
  height: number;
  viewBox: string;
  strokeWidth: number;
  fill?: string;
  stroke?: string;
}

/**
 * Copy icon SVG component
 */
export const CopyIcon: React.FC<{ className?: string }> = ({ className }) => (
  <svg
    width="16"
    height="16"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    className={className}
  >
    <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
    <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
  </svg>
);

/**
 * Checkmark icon SVG component (used when copy succeeds)
 */
export const CheckmarkIcon: React.FC<{ className?: string }> = ({ className }) => (
  <svg
    width="16"
    height="16"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    className={className}
  >
    <polyline points="20,6 9,17 4,12"></polyline>
  </svg>
);

/**
 * Configuration for code block UI elements
 */
export const CODE_BLOCK_CONFIG = {
  // Copy button timeout before resetting to default state (milliseconds)
  COPY_FEEDBACK_DURATION: 2000,
  
  // CSS class names
  CLASSES: {
    CONTAINER: 'code-block-container',
    HEADER: 'code-block-header',
    INFO: 'code-block-info',
    DOTS: 'code-block-dots',
    LANGUAGE: 'code-block-language',
    BUTTON: 'copy-button',
    BUTTON_COPIED: 'copied',
    CONTENT: 'code-block-content',
    INLINE_CODE: 'inline-code',
  },
  
  // Window chrome dots colors (macOS style)
  DOTS: {
    RED: 'red',
    YELLOW: 'yellow',
    GREEN: 'green',
  },
  
  // Button text labels
  TEXT: {
    COPY: 'Copy',
    COPIED: 'Copied!',
  },
  
  // Button titles/aria labels
  TITLES: {
    COPY: 'Copy code',
    COPIED: 'Copied!',
  },
} as const;

/**
 * Syntax highlighter custom style configuration
 */
export const SYNTAX_HIGHLIGHTER_STYLE = {
  margin: 0,
  padding: 0,
  background: 'transparent',
  fontSize: 'inherit',
  lineHeight: 'inherit',
  whiteSpace: 'pre-wrap',
  wordWrap: 'break-word',
  overflowWrap: 'break-word',
} as const;

/**
 * Type for code block inline prop
 */
export type InlineMode = boolean | undefined;

/**
 * Default language for code blocks when none specified
 */
export const DEFAULT_CODE_LANGUAGE = 'text' as const;
