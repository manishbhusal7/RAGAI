import React from 'react';
import CodeBlock from './CodeBlock';

interface MarkdownRendererProps {
  content: string;
}

const MarkdownRenderer: React.FC<MarkdownRendererProps> = ({ content }) => {
  // Enhanced function to parse and render content with code blocks
  const parseContentWithCodeBlocks = (text: string): React.ReactElement[] => {
    const elements: React.ReactElement[] = [];
    
    // Enhanced regex to match code blocks with optional language specification
    const codeBlockRegex = /```(\w+)?\n?([\s\S]*?)```/g;
    const inlineCodeRegex = /`([^`]+)`/g;
    
    let lastIndex = 0;
    let match;
    let elementIndex = 0;

    // Process code blocks first
    while ((match = codeBlockRegex.exec(text)) !== null) {
      const beforeCode = text.slice(lastIndex, match.index);
      
      // Add content before the code block (process inline code in it)
      if (beforeCode) {
        elements.push(
          <div key={`before-${elementIndex}`}>
            {parseInlineElements(beforeCode)}
          </div>
        );
      }

      // Add the code block
      const language = match[1] || 'text';
      const codeContent = match[2].trim();
      
      elements.push(
        <CodeBlock key={`code-${elementIndex}`} language={language}>
          {codeContent}
        </CodeBlock>
      );

      lastIndex = match.index + match[0].length;
      elementIndex++;
    }

    // Add remaining content after the last code block
    if (lastIndex < text.length) {
      const remainingText = text.slice(lastIndex);
      elements.push(
        <div key={`after-${elementIndex}`}>
          {parseInlineElements(remainingText)}
        </div>
      );
    }

    // If no code blocks were found, just process the entire text for inline elements
    if (elements.length === 0) {
      elements.push(
        <div key="content" dangerouslySetInnerHTML={{ __html: formatAiResponseContent(text) }} />
      );
    }

    return elements;
  };

  // Function to handle inline code and other formatting within text
  const parseInlineElements = (text: string): React.ReactElement[] => {
    const elements: React.ReactElement[] = [];
    const inlineCodeRegex = /`([^`]+)`/g;
    
    let lastIndex = 0;
    let match;
    let elementIndex = 0;

    while ((match = inlineCodeRegex.exec(text)) !== null) {
      // Add text before inline code
      if (match.index > lastIndex) {
        const beforeInline = text.slice(lastIndex, match.index);
        elements.push(
          <span 
            key={`text-${elementIndex}`}
            dangerouslySetInnerHTML={{ __html: formatAiResponseContent(beforeInline) }}
          />
        );
      }

      // Add inline code
      elements.push(
        <CodeBlock key={`inline-${elementIndex}`} inline>
          {match[1]}
        </CodeBlock>
      );

      lastIndex = match.index + match[0].length;
      elementIndex++;
    }

    // Add remaining text
    if (lastIndex < text.length) {
      const remainingText = text.slice(lastIndex);
      elements.push(
        <span 
          key={`text-final-${elementIndex}`}
          dangerouslySetInnerHTML={{ __html: formatAiResponseContent(remainingText) }}
        />
      );
    }

    // If no inline code found, just format the entire text
    if (elements.length === 0) {
      return [
        <span 
          key="formatted-text"
          dangerouslySetInnerHTML={{ __html: formatAiResponseContent(text) }}
        />
      ];
    }

    return elements;
  };

  // Enhanced formatting function (keeping the existing logic from Chat.tsx)
  const formatAiResponseContent = (content: string): string => {
    // Step 1: Clean up any leftover HTML tags (except what we want to keep)
    let cleanedContent = content.replace(/<(?!\/?(strong|b|i|em|u|p|br|div|span|h[1-6]|ul|ol|li))[^>]*>/g, '');
    
    // Step 2: Fix common markdown formatting problems
    cleanedContent = cleanedContent
      // Fix malformed numbered lists (remove ** from **1., **2., etc.)
      .replace(/\*\*(\d+\.)/g, '$1')
      // Fix malformed bullet points (remove ** from **•)
      .replace(/\*\*•/g, '•')
      // Fix malformed dashes (remove ** from **-)
      .replace(/\*\*-/g, '-')
      // Remove orphaned ** symbols
      .replace(/\*\*(?=\s|$)/g, '')
      .replace(/^\*\*(?=[A-Za-z])/gm, '');
    
    // Step 3: Apply proper HTML formatting for display
    cleanedContent = cleanedContent
      // Convert headers
      .replace(/^#{3}\s+(.+)$/gm, '<h3 class="response-header">$1</h3>')
      .replace(/^#{2}\s+(.+)$/gm, '<h2 class="response-main-header">$1</h2>')
      
      // Convert proper bold text (only when both ** are present)
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      
      // Convert numbered lists with nice styling
      .replace(/^(\d+\.)\s+(.+)$/gm, '<div class="numbered-item"><span class="number">$1</span> $2</div>')
      
      // Convert bullet points with nice styling
      .replace(/^•\s+(.+)$/gm, '<div class="bullet-item"><span class="bullet">•</span> $1</div>')
      
      // Convert sub-bullets (indented items)
      .replace(/^\s{2,}•\s+(.+)$/gm, '<div class="sub-bullet-item"><span class="bullet">◦</span> $1</div>')
      
      // Convert paragraph breaks
      .replace(/\n\n/g, '</p><p>');
    
    // Step 4: Wrap content in paragraphs and clean up
    cleanedContent = cleanedContent
      .replace(/^(.+)$/gm, (match) => {
        // Don't wrap lines that are already formatted elements
        if (match.includes('<div class=') || match.includes('<h') || match.trim() === '') {
          return match;
        }
        return `<p>${match}</p>`;
      })
      // Remove empty paragraphs and fix spacing
      .replace(/<p><\/p>/g, '')
      .replace(/<\/p><p>/g, '</p>\n<p>')
      .replace(/(<div[^>]*>)/g, '\n$1')
      .replace(/(<\/div>)/g, '$1\n');

    return cleanedContent;
  };

  return (
    <div className="markdown-content">
      {parseContentWithCodeBlocks(content)}
    </div>
  );
};

export default MarkdownRenderer; 