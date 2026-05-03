/**
 * Object utilities for common object operations
 * Provides type-safe object manipulation functions
 */

/**
 * Deep clone an object
 * @param obj - The object to clone
 * @returns Deep clone of the object
 */
export const deepClone = <T>(obj: T): T => {
  if (obj === null || typeof obj !== 'object') {
    return obj;
  }

  if (obj instanceof Date) {
    return new Date(obj.getTime()) as any;
  }

  if (obj instanceof Array) {
    return obj.map(item => deepClone(item)) as any;
  }

  if (obj instanceof Object) {
    const clonedObj = {} as T;
    for (const key in obj) {
      if (obj.hasOwnProperty(key)) {
        (clonedObj as any)[key] = deepClone((obj as any)[key]);
      }
    }
    return clonedObj;
  }

  return obj;
};

/**
 * Merge objects (shallow merge)
 * @param target - Target object
 * @param source - Source object to merge
 * @returns Merged object
 */
export const merge = <T extends object>(target: T, source: Partial<T>): T => {
  return { ...target, ...source };
};

/**
 * Deep merge objects
 * @param target - Target object
 * @param source - Source object to merge
 * @returns Deep merged object
 */
export const deepMerge = <T extends object>(target: T, source: Partial<T>): T => {
  const result = deepClone(target);

  for (const key in source) {
    if (source.hasOwnProperty(key)) {
      const targetValue = (result as any)[key];
      const sourceValue = (source as any)[key];

      if (sourceValue === null || sourceValue === undefined) {
        continue;
      }

      if (typeof targetValue === 'object' && typeof sourceValue === 'object') {
        (result as any)[key] = deepMerge(targetValue, sourceValue);
      } else {
        (result as any)[key] = sourceValue;
      }
    }
  }

  return result;
};

/**
 * Get value from nested object
 * @param obj - Object to search
 * @param path - Path to value (e.g., "user.profile.name")
 * @param defaultValue - Default value if not found
 * @returns Value or default
 */
export const getValue = <T>(
  obj: any,
  path: string,
  defaultValue?: T
): T | undefined => {
  const keys = path.split('.');
  let current = obj;

  for (const key of keys) {
    if (current == null || typeof current !== 'object') {
      return defaultValue;
    }
    current = (current as any)[key];
  }

  return current ?? defaultValue;
};

/**
 * Set value in nested object
 * @param obj - Object to modify
 * @param path - Path to value (e.g., "user.profile.name")
 * @param value - Value to set
 * @returns Modified object
 */
export const setValue = <T extends object>(
  obj: T,
  path: string,
  value: unknown
): T => {
  const clone = deepClone(obj);
  const keys = path.split('.');
  let current: any = clone;

  for (let i = 0; i < keys.length - 1; i++) {
    const key = keys[i];
    if (!(key in current) || typeof current[key] !== 'object') {
      current[key] = {};
    }
    current = current[key];
  }

  current[keys[keys.length - 1]] = value;
  return clone;
};

/**
 * Pick specific keys from object
 * @param obj - Object to pick from
 * @param keys - Keys to pick
 * @returns New object with only picked keys
 */
export const pick = <T extends object, K extends keyof T>(
  obj: T,
  ...keys: K[]
): Pick<T, K> => {
  const result = {} as Pick<T, K>;
  keys.forEach(key => {
    result[key] = obj[key];
  });
  return result;
};

/**
 * Omit specific keys from object
 * @param obj - Object to omit from
 * @param keys - Keys to omit
 * @returns New object without omitted keys
 */
export const omit = <T extends object, K extends keyof T>(
  obj: T,
  ...keys: K[]
): Omit<T, K> => {
  const result = { ...obj } as Omit<T, K>;
  keys.forEach(key => {
    delete (result as any)[key];
  });
  return result;
};
