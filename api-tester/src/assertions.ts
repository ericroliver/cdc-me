/**
 * Assertion Engine
 */

import { JSONPath } from 'jsonpath-plus';
import Ajv from 'ajv';
import { ExpectDefinition, AssertionResult } from './types.js';

const AjvClass = Ajv.default || Ajv;
const ajv = new AjvClass();

/**
 * Run all assertions against a response
 */
export function runAssertions(
  expect: ExpectDefinition,
  response: { status: number; headers: Record<string, string>; body: unknown }
): AssertionResult[] {
  const results: AssertionResult[] = [];

  // Status assertion
  if (expect.status !== undefined) {
    const expectedStatuses = Array.isArray(expect.status) ? expect.status : [expect.status];
    results.push({
      type: 'status',
      expected: expect.status,
      actual: response.status,
      passed: expectedStatuses.includes(response.status),
      message: expectedStatuses.includes(response.status)
        ? undefined
        : `Expected status ${expect.status}, got ${response.status}`
    });
  }

  // Header assertions
  if (expect.headers) {
    for (const [header, expected] of Object.entries(expect.headers)) {
      const actual = response.headers[header.toLowerCase()];
      const passed = expected instanceof RegExp
        ? expected.test(actual || '')
        : actual === expected;
      
      results.push({
        type: 'header',
        path: header,
        expected,
        actual,
        passed,
        message: passed ? undefined : `Header '${header}' mismatch`
      });
    }
  }

  // Body assertions
  if (expect.body) {
    // Exact match
    if (expect.body.equals !== undefined) {
      const passed = JSON.stringify(response.body) === JSON.stringify(expect.body.equals);
      results.push({
        type: 'body.equals',
        expected: expect.body.equals,
        actual: response.body,
        passed,
        message: passed ? undefined : 'Body does not match expected value'
      });
    }

    // Contains (for string bodies or stringified JSON)
    if (expect.body.contains) {
      const bodyStr = typeof response.body === 'string'
        ? response.body
        : JSON.stringify(response.body);
      
      for (const substring of expect.body.contains) {
        const passed = bodyStr.includes(substring);
        results.push({
          type: 'body.contains',
          expected: substring,
          actual: passed ? substring : `(not found in response)`,
          passed,
          message: passed ? undefined : `Body does not contain '${substring}'`
        });
      }
    }

    // Not contains
    if (expect.body.notContains) {
      const bodyStr = typeof response.body === 'string'
        ? response.body
        : JSON.stringify(response.body);
      
      for (const substring of expect.body.notContains) {
        const passed = !bodyStr.includes(substring);
        results.push({
          type: 'body.notContains',
          expected: `not '${substring}'`,
          actual: passed ? '(not found)' : `found '${substring}'`,
          passed,
          message: passed ? undefined : `Body should not contain '${substring}'`
        });
      }
    }

    // JSONPath assertions
    if (expect.body.jsonPath) {
      for (const [path, expected] of Object.entries(expect.body.jsonPath)) {
        const matches = JSONPath({ path, json: response.body as object });
        const actual = matches.length === 1 ? matches[0] : matches;
        const passed = JSON.stringify(actual) === JSON.stringify(expected);
        
        results.push({
          type: 'body.jsonPath',
          path,
          expected,
          actual,
          passed,
          message: passed ? undefined : `JSONPath '${path}' mismatch`
        });
      }
    }

    // JSON Schema validation
    if (expect.body.schema) {
      const validate = ajv.compile(expect.body.schema);
      const valid = validate(response.body);
      
      results.push({
        type: 'body.schema',
        expected: 'valid against schema',
        actual: valid ? 'valid' : ajv.errorsText(validate.errors),
        passed: valid,
        message: valid ? undefined : `Schema validation failed: ${ajv.errorsText(validate.errors)}`
      });
    }
  }

  return results;
}

/**
 * Extract values from response using JSONPath for capture
 */
export function captureValues(
  capture: Record<string, string>,
  responseBody: unknown
): Record<string, unknown> {
  const captured: Record<string, unknown> = {};
  
  for (const [varName, jsonPath] of Object.entries(capture)) {
    const matches = JSONPath({ path: jsonPath, json: responseBody as object });
    captured[varName] = matches.length === 1 ? matches[0] : matches;
  }
  
  return captured;
}
