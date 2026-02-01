/**
 * YAML Test Loader
 */

import { readFileSync } from 'fs';
import { glob } from 'glob';
import YAML from 'yaml';
import { TestDefinition, TestEnvironment } from './types.js';

/**
 * Load a single test file
 */
export function loadTestFile(filePath: string): TestDefinition {
  const content = readFileSync(filePath, 'utf-8');
  const test = YAML.parse(content) as TestDefinition;
  
  if (!test.name) {
    throw new Error(`Test file ${filePath} missing required 'name' field`);
  }
  if (!test.request) {
    throw new Error(`Test file ${filePath} missing required 'request' field`);
  }
  if (!test.expect) {
    throw new Error(`Test file ${filePath} missing required 'expect' field`);
  }
  
  return test;
}

/**
 * Load all test files from a directory
 */
export async function loadTestFiles(pattern: string): Promise<{ file: string; test: TestDefinition }[]> {
  const files = await glob(pattern);
  return files.map(file => ({
    file,
    test: loadTestFile(file)
  }));
}

/**
 * Interpolate environment variables in a string
 * Supports ${VAR} and ${VAR:-default} syntax
 */
export function interpolate(value: string, env: TestEnvironment): string {
  return value.replace(/\$\{([^}]+)\}/g, (match, expr) => {
    const [varName, defaultValue] = expr.split(':-');
    const envValue = env.variables[varName];
    
    if (envValue !== undefined) {
      return String(envValue);
    }
    if (defaultValue !== undefined) {
      return defaultValue;
    }
    // Check process.env as fallback
    if (process.env[varName]) {
      return process.env[varName]!;
    }
    return match; // Keep original if not found
  });
}

/**
 * Deep interpolate all strings in an object
 */
export function interpolateDeep<T>(obj: T, env: TestEnvironment): T {
  if (typeof obj === 'string') {
    return interpolate(obj, env) as T;
  }
  if (Array.isArray(obj)) {
    return obj.map(item => interpolateDeep(item, env)) as T;
  }
  if (obj && typeof obj === 'object') {
    const result: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(obj)) {
      result[key] = interpolateDeep(value, env);
    }
    return result as T;
  }
  return obj;
}
