/**
 * API Test Definition Types
 */

export interface TestDefinition {
  name: string;
  description?: string;
  skip?: boolean;
  request: RequestDefinition;
  expect: ExpectDefinition;
  capture?: CaptureDefinition;
}

export interface RequestDefinition {
  method: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  url: string;
  headers?: Record<string, string>;
  query?: Record<string, string>;
  body?: unknown;
  timeout?: number;
}

export interface ExpectDefinition {
  status?: number | number[];
  headers?: Record<string, string | RegExp>;
  body?: BodyAssertion;
}

export interface BodyAssertion {
  // Exact match
  equals?: unknown;
  // Contains these strings (for string bodies)
  contains?: string[];
  // Does not contain these strings
  notContains?: string[];
  // JSONPath assertions: { "$.path": expectedValue }
  jsonPath?: Record<string, unknown>;
  // JSON Schema validation
  schema?: object;
}

export interface CaptureDefinition {
  // Capture values from response into environment
  // { "envVarName": "$.jsonPath" }
  [envVar: string]: string;
}

export interface TestResult {
  name: string;
  file: string;
  passed: boolean;
  duration: number;
  request: {
    method: string;
    url: string;
  };
  response?: {
    status: number;
    body: unknown;
  };
  assertions: AssertionResult[];
  captured?: Record<string, unknown>;
  error?: string;
}

export interface AssertionResult {
  type: string;
  path?: string;
  expected: unknown;
  actual: unknown;
  passed: boolean;
  message?: string;
}

export interface TestEnvironment {
  baseUrl: string;
  variables: Record<string, unknown>;
}
