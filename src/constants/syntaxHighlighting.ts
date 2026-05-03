/**
 * Syntax highlighting language registry
 * Centralized configuration for all supported languages in code blocks
 */

import javascript from 'react-syntax-highlighter/dist/esm/languages/prism/javascript';
import typescript from 'react-syntax-highlighter/dist/esm/languages/prism/typescript';
import python from 'react-syntax-highlighter/dist/esm/languages/prism/python';
import java from 'react-syntax-highlighter/dist/esm/languages/prism/java';
import csharp from 'react-syntax-highlighter/dist/esm/languages/prism/csharp';
import sql from 'react-syntax-highlighter/dist/esm/languages/prism/sql';
import json from 'react-syntax-highlighter/dist/esm/languages/prism/json';
import css from 'react-syntax-highlighter/dist/esm/languages/prism/css';
import html from 'react-syntax-highlighter/dist/esm/languages/prism/markup';
import bash from 'react-syntax-highlighter/dist/esm/languages/prism/bash';

/**
 * Language registry mapping language names to their syntax highlighter imports
 */
export const LANGUAGE_REGISTRY = {
  javascript,
  typescript,
  python,
  java,
  csharp,
  sql,
  json,
  css,
  html,
  bash,
} as const;

/**
 * List of all supported language identifiers
 */
export const SUPPORTED_LANGUAGES = Object.keys(LANGUAGE_REGISTRY) as const;

/**
 * Type for supported language identifiers
 */
export type SupportedLanguage = typeof SUPPORTED_LANGUAGES[number];

/**
 * Check if a language is supported
 * @param language - The language identifier to check
 * @returns true if the language is supported
 */
export const isSupportedLanguage = (language: string): language is SupportedLanguage => {
  return SUPPORTED_LANGUAGES.includes(language as SupportedLanguage);
};

/**
 * Get the human-readable name for a language
 * Useful for displaying language names in UI
 * @param language - The language identifier
 * @returns Human-readable language name
 */
export const getLanguageDisplayName = (language: string): string => {
  const displayNames: Record<string, string> = {
    javascript: 'JavaScript',
    typescript: 'TypeScript',
    python: 'Python',
    java: 'Java',
    csharp: 'C#',
    sql: 'SQL',
    json: 'JSON',
    css: 'CSS',
    html: 'HTML',
    bash: 'Bash',
  };

  return displayNames[language] || language;
};
