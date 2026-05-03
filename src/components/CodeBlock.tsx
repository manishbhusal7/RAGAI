import React, { useState, useCallback } from 'react';
import { Light as SyntaxHighlighter } from 'react-syntax-highlighter';
import { tomorrow } from 'react-syntax-highlighter/dist/esm/styles/prism';
import './CodeBlock.css';
import { LANGUAGE_REGISTRY } from '../constants/syntaxHighlighting';
import { 
  CODE_BLOCK_CONFIG, 
  SYNTAX_HIGHLIGHTER_STYLE, 
  DEFAULT_CODE_LANGUAGE,
  CopyIcon,
  CheckmarkIcon 
} from '../constants/codeBlock';

// Register all supported languages
Object.entries(LANGUAGE_REGISTRY).forEach(([language, config]) => {
  SyntaxHighlighter.registerLanguage(language, config);
});

interface CodeBlockProps {
  /** Code content to display */
  children: string;
  /** Programming language for syntax highlighting */
  language?: string;
  /** Whether to render as inline code */
  inline?: boolean;
}

const CodeBlock: React.FC<CodeBlockProps> = ({ 
  children, 
  language = DEFAULT_CODE_LANGUAGE, 
  inline = false 
}) => {
  const [copied, setCopied] = useState(false);

  const copyToClipboard = useCallback(async () => {
    try {
      await navigator.clipboard.writeText(children);
      setCopied(true);
      setTimeout(() => setCopied(false), CODE_BLOCK_CONFIG.COPY_FEEDBACK_DURATION);
    } catch (err) {
      console.error('Failed to copy text: ', err);
    }
  }, [children]);

  // For inline code
  if (inline) {
    return (
      <code className={CODE_BLOCK_CONFIG.CLASSES.INLINE_CODE}>
        {children}
      </code>
    );
  }

  // For code blocks
  return (
    <div className={CODE_BLOCK_CONFIG.CLASSES.CONTAINER}>
      <div className={CODE_BLOCK_CONFIG.CLASSES.HEADER}>
        <div className={CODE_BLOCK_CONFIG.CLASSES.INFO}>
          <div className={CODE_BLOCK_CONFIG.CLASSES.DOTS}>
            <span className={`dot ${CODE_BLOCK_CONFIG.DOTS.RED}`}></span>
            <span className={`dot ${CODE_BLOCK_CONFIG.DOTS.YELLOW}`}></span>
            <span className={`dot ${CODE_BLOCK_CONFIG.DOTS.GREEN}`}></span>
          </div>
          <span className={CODE_BLOCK_CONFIG.CLASSES.LANGUAGE}>{language}</span>
        </div>
        <button 
          className={`${CODE_BLOCK_CONFIG.CLASSES.BUTTON} ${copied ? CODE_BLOCK_CONFIG.CLASSES.BUTTON_COPIED : ''}`}
          onClick={copyToClipboard}
          title={copied ? CODE_BLOCK_CONFIG.TITLES.COPIED : CODE_BLOCK_CONFIG.TITLES.COPY}
        >
          {copied ? <CheckmarkIcon /> : <CopyIcon />}
          <span className="copy-button-text">{copied ? CODE_BLOCK_CONFIG.TEXT.COPIED : CODE_BLOCK_CONFIG.TEXT.COPY}</span>
        </button>
      </div>
      <div className={CODE_BLOCK_CONFIG.CLASSES.CONTENT}>
        <SyntaxHighlighter
          language={language}
          style={tomorrow}
          customStyle={SYNTAX_HIGHLIGHTER_STYLE as React.CSSProperties}
          codeTagProps={{
            style: {
              fontFamily: '"Fira Code", "Consolas", "Monaco", "Courier New", monospace',
              background: 'transparent',
              padding: 0,
              margin: 0,
              whiteSpace: 'pre-wrap',
              wordWrap: 'break-word',
              overflowWrap: 'break-word',
            }
          }}
          showLineNumbers={false}
          wrapLines={true}
          wrapLongLines={true}
        >
          {children}
        </SyntaxHighlighter>
      </div>
    </div>
  );
};

export default CodeBlock;
