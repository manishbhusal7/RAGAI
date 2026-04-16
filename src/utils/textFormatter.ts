// Professional AI response formatter - mimics ChatGPT/Claude style
export const formatAIResponse = (text: string): string => {
  if (!text) return text;

  let formatted = text;

  // Clean basic formatting
  formatted = formatted.replace(/^#{1,6}\s*/gm, '');
  formatted = formatted.replace(/\*\*(.*?)\*\*/g, '$1');
  formatted = formatted.replace(/\*(.*?)\*/g, '$1');
  formatted = formatted.replace(/\[([^\]]+)\]/g, '$1');
  // Remove any non-standard characters and symbols
  formatted = formatted.replace(/[^\x20-\x7E\n]/g, '');
  formatted = formatted.replace(/\s{2,}/g, ' ');
  formatted = formatted.trim();

  return formatted;
};

// Advanced formatting that structures content like professional LLMs
export const formatForReadability = (text: string): string => {
  let formatted = formatAIResponse(text);
  
  // Apply universal smart formatting for any content type
  return formatUniversalContent(formatted);
};

// Universal content formatter that works for any document type
const formatUniversalContent = (text: string): string => {
  // Split into logical sections and format each
  let formatted = text;
  
  // 1. Break up very long paragraphs
  formatted = breakLongParagraphs(formatted);
  
  // 2. Format lists and structured content
  formatted = formatLists(formatted);
  
  // 3. Add proper spacing between sections
  formatted = improveSpacing(formatted);
  
  // 4. Format headings and emphasis
  formatted = formatHeadings(formatted);
  
  return formatted.trim();
};

// Break up paragraphs that are too long
const breakLongParagraphs = (text: string): string => {
  const sentences = text.split(/(?<=[.!?])\s+/);
  const paragraphs = [];
  let currentParagraph = '';
  
  for (const sentence of sentences) {
    // If adding this sentence would make the paragraph too long, start a new one
    if (currentParagraph.length + sentence.length > 200 && currentParagraph) {
      paragraphs.push(currentParagraph.trim());
      currentParagraph = sentence;
    } else {
      currentParagraph += (currentParagraph ? ' ' : '') + sentence;
    }
  }
  
  if (currentParagraph) {
    paragraphs.push(currentParagraph.trim());
  }
  
  return paragraphs.join('\n\n');
};

// Format any type of list (numbered, bulleted, or dash-separated)
const formatLists = (text: string): string => {
  let formatted = text;
  
  // Format numbered lists (1. 2. 3.)
  formatted = formatted.replace(/(\d+\.\s*[^\n]+)/g, (match, item) => {
    return item.trim();
  });
  
  // Format items separated by " - " (common in many documents)
  formatted = formatted.replace(/\s+-\s+([^\n]+)/g, '\n• $1');
  
  // Clean up existing bullet points
  formatted = formatted.replace(/^\s*[•\-\*]\s*/gm, '• ');
  
  return formatted;
};

// Improve spacing between logical sections
const improveSpacing = (text: string): string => {
  let formatted = text;
  
  // Add spacing before numbered items
  formatted = formatted.replace(/([.!?])\s*(\d+\.)/g, '$1\n\n$2');
  
  // Add spacing before bullet points when they follow text
  formatted = formatted.replace(/([.!?])\s*(•)/g, '$1\n\n$2');
  
  // Clean up excessive spacing
  formatted = formatted.replace(/\n{3,}/g, '\n\n');
  
  return formatted;
};

// Format headings and emphasis naturally
const formatHeadings = (text: string): string => {
  let formatted = text;
  
  // Detect natural headings (words followed by colon)
  formatted = formatted.replace(/^([A-Z][A-Za-z\s&]+):\s*/gm, '**$1:**\n');
  
  // Format section breaks
  formatted = formatted.replace(/(\d+\.\s*)([A-Z][A-Za-z\s]+)/g, '\n**$1$2**');
  
  return formatted;
};

// Add context indicator for document usage (subtle and professional)
export const addContextIndicator = (text: string, hasDocuments: boolean): string => {
  const formattedText = formatForReadability(text);
  
  if (hasDocuments) {
    // Check if the response already mentions document context
    const lowerText = formattedText.toLowerCase();
    if (lowerText.includes('based on') || 
        lowerText.includes('according to') ||
        lowerText.includes('document') ||
        lowerText.includes('resume')) {
      return formattedText;
    }
    return `Based on the uploaded document:\n\n${formattedText}`;
  }
  
  return formattedText;
}; 