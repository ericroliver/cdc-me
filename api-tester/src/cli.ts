#!/usr/bin/env node
/**
 * API Tester CLI
 */

import { parseArgs } from 'node:util';
import chalk from 'chalk';
import { loadTestFiles } from './loader.js';
import { runTests } from './runner.js';
import { TestResult, TestEnvironment } from './types.js';

function printUsage() {
  console.log(`
${chalk.bold('api-tester')} - YAML-driven API test harness

${chalk.bold('USAGE')}
  api-tester [options] <test-pattern>

${chalk.bold('OPTIONS')}
  -b, --base-url <url>    Base URL for API requests (default: http://localhost:8080)
  -e, --env <key=value>   Set environment variable (repeatable)
  -v, --verbose           Show response bodies on failure
  -h, --help              Show this help

${chalk.bold('EXAMPLES')}
  api-tester tests/*.yaml
  api-tester -b http://localhost:3000 tests/snapshots/*.yaml
  api-tester -e API_KEY=secret -e DB_NAME=testdb tests/*.yaml
`);
}

function printResult(result: TestResult, verbose: boolean) {
  const icon = result.error === 'SKIPPED' ? chalk.yellow('○') 
    : result.passed ? chalk.green('✓') 
    : chalk.red('✗');
  
  const duration = chalk.gray(`(${result.duration}ms)`);
  const file = chalk.gray(result.file);
  
  console.log(`${icon} ${result.name} ${duration}`);
  console.log(`  ${file}`);

  if (!result.passed && result.error !== 'SKIPPED') {
    if (result.error) {
      console.log(chalk.red(`  Error: ${result.error}`));
    }
    
    for (const assertion of result.assertions) {
      if (!assertion.passed) {
        console.log(chalk.red(`  ✗ ${assertion.type}${assertion.path ? ` [${assertion.path}]` : ''}`));
        console.log(chalk.gray(`    Expected: ${JSON.stringify(assertion.expected)}`));
        console.log(chalk.gray(`    Actual:   ${JSON.stringify(assertion.actual)}`));
      }
    }

    if (verbose && result.response) {
      console.log(chalk.gray(`  Response (${result.response.status}):`));
      console.log(chalk.gray(`    ${JSON.stringify(result.response.body, null, 2).split('\n').join('\n    ')}`));
    }
  }

  if (result.captured && Object.keys(result.captured).length > 0) {
    console.log(chalk.cyan(`  Captured: ${JSON.stringify(result.captured)}`));
  }

  console.log();
}

function printSummary(results: TestResult[]) {
  const passed = results.filter(r => r.passed && r.error !== 'SKIPPED').length;
  const failed = results.filter(r => !r.passed).length;
  const skipped = results.filter(r => r.error === 'SKIPPED').length;
  const total = results.length;
  const totalDuration = results.reduce((sum, r) => sum + r.duration, 0);

  console.log(chalk.bold('─'.repeat(50)));
  
  if (failed === 0) {
    console.log(chalk.green.bold(`✓ ${passed} passed`) + 
      (skipped > 0 ? chalk.yellow(`, ${skipped} skipped`) : '') +
      chalk.gray(` (${totalDuration}ms)`));
  } else {
    console.log(
      chalk.red.bold(`✗ ${failed} failed`) + 
      chalk.green(`, ${passed} passed`) +
      (skipped > 0 ? chalk.yellow(`, ${skipped} skipped`) : '') +
      chalk.gray(` (${totalDuration}ms)`)
    );
  }
}

async function main() {
  const { values, positionals } = parseArgs({
    options: {
      'base-url': { type: 'string', short: 'b', default: 'http://localhost:8080' },
      'env': { type: 'string', short: 'e', multiple: true, default: [] },
      'verbose': { type: 'boolean', short: 'v', default: false },
      'help': { type: 'boolean', short: 'h', default: false },
    },
    allowPositionals: true
  });

  if (values.help || positionals.length === 0) {
    printUsage();
    process.exit(values.help ? 0 : 1);
  }

  // Build environment
  const env: TestEnvironment = {
    baseUrl: values['base-url']!,
    variables: {}
  };

  // Parse -e KEY=VALUE options
  for (const envStr of values.env || []) {
    const [key, ...valueParts] = envStr.split('=');
    if (key && valueParts.length > 0) {
      env.variables[key] = valueParts.join('=');
    }
  }

  // Load tests
  const pattern = positionals[0];
  console.log(chalk.bold(`\nLoading tests: ${pattern}\n`));
  
  const tests = await loadTestFiles(pattern);
  
  if (tests.length === 0) {
    console.log(chalk.yellow('No test files found.'));
    process.exit(1);
  }

  console.log(chalk.gray(`Found ${tests.length} test(s)\n`));
  console.log(chalk.bold('─'.repeat(50)));
  console.log();

  // Run tests
  const results = await runTests(tests, env);

  // Print results
  for (const result of results) {
    printResult(result, values.verbose || false);
  }

  // Print summary
  printSummary(results);

  // Exit with failure code if any tests failed
  const failed = results.some(r => !r.passed);
  process.exit(failed ? 1 : 0);
}

main().catch(err => {
  console.error(chalk.red('Fatal error:'), err);
  process.exit(1);
});
