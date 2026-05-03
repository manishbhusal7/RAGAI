/**
 * Logging utilities for consistent debugging and monitoring
 * Provides structured logging throughout the application
 */

/**
 * Log levels for filtering and severity
 */
export enum LogLevel {
  DEBUG = 'DEBUG',
  INFO = 'INFO',
  WARN = 'WARN',
  ERROR = 'ERROR',
}

/**
 * Structured logger for application events
 */
export class Logger {
  private readonly context: string;
  private minLevel: LogLevel = LogLevel.DEBUG;

  constructor(context: string) {
    this.context = context;
  }

  /**
   * Set minimum log level for filtering
   */
  setMinLevel(level: LogLevel): void {
    this.minLevel = level;
  }

  /**
   * Log a debug message
   */
  debug(message: string, data?: unknown): void {
    this.log(LogLevel.DEBUG, message, data);
  }

  /**
   * Log an info message
   */
  info(message: string, data?: unknown): void {
    this.log(LogLevel.INFO, message, data);
  }

  /**
   * Log a warning message
   */
  warn(message: string, data?: unknown): void {
    this.log(LogLevel.WARN, message, data);
  }

  /**
   * Log an error message
   */
  error(message: string, error?: Error | unknown): void {
    this.log(LogLevel.ERROR, message, error);
  }

  /**
   * Core logging function
   */
  private log(level: LogLevel, message: string, data?: unknown): void {
    // Skip if below minimum level
    const levels = [LogLevel.DEBUG, LogLevel.INFO, LogLevel.WARN, LogLevel.ERROR];
    if (levels.indexOf(level) < levels.indexOf(this.minLevel)) {
      return;
    }

    const timestamp = new Date().toISOString();
    const prefix = `[${timestamp}] [${this.context}] [${level}]`;
    const fullMessage = `${prefix} ${message}`;

    switch (level) {
      case LogLevel.DEBUG:
        console.debug(fullMessage, data);
        break;
      case LogLevel.INFO:
        console.info(fullMessage, data);
        break;
      case LogLevel.WARN:
        console.warn(fullMessage, data);
        break;
      case LogLevel.ERROR:
        console.error(fullMessage, data);
        break;
    }
  }
}

/**
 * Create a logger for a specific context
 * @param context - The context name (usually the module name)
 * @returns Logger instance
 */
export const createLogger = (context: string): Logger => {
  return new Logger(context);
};

/**
 * Global logger instance
 */
export const logger = createLogger('CopyRAG');
