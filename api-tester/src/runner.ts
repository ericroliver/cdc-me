/**
 * Test Runner
 */

import axios, { AxiosError } from 'axios';
import { TestDefinition, TestResult, TestEnvironment } from './types.js';
import { runAssertions, captureValues } from './assertions.js';
import { interpolateDeep } from './loader.js';

/**
 * Execute a single test
 */
export async function runTest(
  test: TestDefinition,
  file: string,
  env: TestEnvironment
): Promise<TestResult> {
  const startTime = Date.now();
  
  // Skip if marked
  if (test.skip) {
    return {
      name: test.name,
      file,
      passed: true,
      duration: 0,
      request: { method: test.request.method, url: test.request.url },
      assertions: [],
      error: 'SKIPPED'
    };
  }

  // Interpolate variables in test definition
  const interpolatedTest = interpolateDeep(test, env);
  const { request, expect, capture } = interpolatedTest;

  // Build full URL
  const fullUrl = request.url.startsWith('http')
    ? request.url
    : `${env.baseUrl}${request.url}`;

  try {
    // Execute request
    const response = await axios({
      method: request.method,
      url: fullUrl,
      headers: request.headers,
      params: request.query,
      data: request.body,
      timeout: request.timeout || 30000,
      validateStatus: () => true, // Don't throw on any status
    });

    // Normalize headers to lowercase
    const normalizedHeaders: Record<string, string> = {};
    for (const [key, value] of Object.entries(response.headers)) {
      normalizedHeaders[key.toLowerCase()] = String(value);
    }

    // Run assertions
    const assertions = runAssertions(expect, {
      status: response.status,
      headers: normalizedHeaders,
      body: response.data
    });

    // Capture values if specified
    let captured: Record<string, unknown> | undefined;
    if (capture) {
      captured = captureValues(capture, response.data);
      // Add captured values to environment for subsequent tests
      Object.assign(env.variables, captured);
    }

    const passed = assertions.every(a => a.passed);

    return {
      name: test.name,
      file,
      passed,
      duration: Date.now() - startTime,
      request: { method: request.method, url: fullUrl },
      response: { status: response.status, body: response.data },
      assertions,
      captured
    };

  } catch (error) {
    const axiosError = error as AxiosError;
    return {
      name: test.name,
      file,
      passed: false,
      duration: Date.now() - startTime,
      request: { method: request.method, url: fullUrl },
      assertions: [],
      error: axiosError.message || String(error)
    };
  }
}

/**
 * Run multiple tests in sequence
 */
export async function runTests(
  tests: { file: string; test: TestDefinition }[],
  env: TestEnvironment
): Promise<TestResult[]> {
  const results: TestResult[] = [];

  for (const { file, test } of tests) {
    const result = await runTest(test, file, env);
    results.push(result);
  }

  return results;
}
