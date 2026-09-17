import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { commitBump, nextVersion, resolveRelease } from './release-version.mjs';
import { checkSarif, checkDirectory } from './check-codeql.mjs';

test('all conventional commit rules, scopes and breaking changes', () => {
  const types = { feat: 2, fix: 1, perf: 1, refactor: 0, docs: 0, test: 0, style: 0, build: 0, ci: 0, chore: 0 };
  for (const [type, bump] of Object.entries(types)) {
    assert.equal(commitBump(`${type}: description`), bump);
    assert.equal(commitBump(`${type}(auth): description`), bump);
    assert.equal(commitBump(`${type}!: description`), 3);
    assert.equal(commitBump(`${type}(auth)!: description`), 3);
  }
  assert.equal(commitBump('custom!: breaking change'), 3);
  assert.equal(commitBump('Merge pull request #42'), 0);
  assert.equal(commitBump('not conventional'), 0);
  assert.equal(nextVersion('1.9.9', 3), '2.0.0');
  assert.equal(nextVersion('1.9.9', 2), '1.10.0');
  assert.equal(nextVersion('1.9.9', 1), '1.9.10');
  assert.equal(nextVersion('1.9.9', 0), '1.9.9');
});

test('shared releases, numeric tag ordering, merges, reruns and stale commits', () => {
  const cwd = mkdtempSync(join(tmpdir(), 'release-test-'));
  const git = (...args) => execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  let revision = 0;
  const commit = (directory, subject) => {
    mkdirSync(join(cwd, directory), { recursive: true });
    writeFileSync(join(cwd, directory, 'file.txt'), String(++revision));
    git('add', '.');
    git('commit', '-m', subject);
  };
  try {
    git('init', '-b', 'master');
    git('config', 'user.email', 'test@example.com');
    git('config', 'user.name', 'Pipeline test');
    git('config', 'commit.gpgsign', 'false');
    commit('frontend', 'docs: initial');
    assert.equal(resolveRelease(cwd).changed, false);
    commit('backend', 'fix: first backend');
    assert.equal(resolveRelease(cwd).version, '0.0.1');
    git('tag', 'v1.9.0');
    git('tag', 'v1.10.0');
    git('tag', 'v1.8.0');
    const old = git('rev-parse', 'HEAD');
    commit('backend', 'perf: faster');
    assert.deepEqual(resolveRelease(cwd), {
      changed: true, version: '1.10.1', tag: 'v1.10.1', previous: 'v1.10.0',
    });
    git('checkout', '-b', 'feature');
    commit('frontend', 'feat: new UI');
    git('checkout', 'master');
    git('merge', '--no-ff', 'feature', '-m', 'Merge pull request #1');
    assert.equal(resolveRelease(cwd).version, '1.11.0');
    commit('frontend', 'chore!: incompatible UI');
    assert.equal(resolveRelease(cwd).version, '2.0.0');
    git('tag', 'v2.0.0');
    assert.equal(resolveRelease(cwd).changed, false);
    commit('frontend', 'docs: explanation');
    assert.equal(resolveRelease(cwd).changed, false);
    git('checkout', '-b', 'backend-only');
    commit('backend', 'docs: backend notes');
    git('checkout', 'master');
    commit('frontend', 'docs: frontend notes');
    git('merge', '--no-ff', 'backend-only', '-m', 'feat: backend feature');
    assert.equal(resolveRelease(cwd).version, '2.1.0');
    git('tag', 'v2.1.0');
    commit('.', 'fix: shared configuration');
    assert.equal(resolveRelease(cwd).version, '2.1.1');
    git('tag', 'v2.1.1');
    commit('frontend', 'fix: UI only');
    assert.deepEqual(resolveRelease(cwd), {
      changed: true, version: '2.1.2', tag: 'v2.1.2', previous: 'v2.1.1',
    });
    git('tag', 'v99.0.0-preview');
    assert.equal(resolveRelease(cwd).version, '2.1.2');
    git('checkout', old);
    assert.throws(() => resolveRelease(cwd));
  } finally {
    rmSync(cwd, { recursive: true, force: true });
  }
});

const sarif = (severity, level = 'warning') => ({
  version: '2.1.0',
  runs: [{
    tool: { driver: { name: 'CodeQL', rules: [{ id: 'rule', properties: { 'security-severity': severity }, defaultConfiguration: { level } }] } },
    results: [{ ruleId: 'rule' }],
  }],
});

test('CodeQL blocks high, critical and errors, including rule default levels', () => {
  for (const severity of ['7.0', '8.9', '9.0', '10.0']) assert.deepEqual(checkSarif(sarif(severity)), ['rule']);
  for (const severity of [undefined, '0.0', '4.0', '6.9']) assert.deepEqual(checkSarif(sarif(severity)), []);
  assert.deepEqual(checkSarif(sarif(undefined, 'error')), ['rule']);
  const explicit = sarif('4.0');
  explicit.runs[0].results[0].level = 'error';
  assert.deepEqual(checkSarif(explicit), ['rule']);
  const indexed = sarif('9.0');
  indexed.runs[0].results = [{ ruleIndex: 0 }];
  assert.deepEqual(checkSarif(indexed), ['rule']);
});

test('CodeQL resolves rules in query packs using the result component reference', () => {
  const grouped = sarif('0');
  const run = grouped.runs[0];
  // Reuse the same ID/index in the driver and another pack to catch accidental
  // fallback to metadata from a different component.
  run.tool.extensions = [
    { name: 'other-pack', rules: run.tool.driver.rules },
    { name: 'codeql/javascript-queries', rules: sarif('4').runs[0].tool.driver.rules },
  ];
  run.results = [{ ruleId: 'rule', rule: { id: 'rule', index: 0, toolComponent: { index: 1 } } }];
  assert.deepEqual(checkSarif(grouped), []);
  const rule = run.tool.extensions[1].rules[0];
  for (const severity of ['7', '9']) {
    rule.properties['security-severity'] = severity;
    assert.deepEqual(checkSarif(grouped), ['rule']);
  }
  rule.properties['security-severity'] = '0';
  rule.defaultConfiguration.level = 'error';
  assert.deepEqual(checkSarif(grouped), ['rule']);
  run.tool.driver.rules = [];
  assert.deepEqual(checkSarif(grouped), ['rule']);
  run.results[0].rule = { id: 'rule', toolComponent: { name: 'codeql/javascript-queries' } };
  assert.deepEqual(checkSarif(grouped), ['rule']);
  run.results[0].rule.toolComponent = { index: 99 };
  assert.throws(() => checkSarif(grouped), /rule metadata/);
  run.results[0].rule = { id: 'wrong-id', index: 0, toolComponent: { index: 1 } };
  assert.throws(() => checkSarif(grouped), /rule metadata/);
});

test('CodeQL fails closed for missing output, invalid metadata and execution errors', () => {
  assert.throws(() => checkSarif({ version: '2.1.0', runs: [] }));
  assert.throws(() => checkSarif(sarif('invalid')));
  const failed = sarif('0');
  failed.runs[0].invocations = [{ executionSuccessful: false }];
  assert.throws(() => checkSarif(failed));
  failed.runs[0].invocations = [{ executionSuccessful: true, toolExecutionNotifications: [{ level: 'error' }] }];
  assert.throws(() => checkSarif(failed));
  delete failed.runs[0].results;
  assert.throws(() => checkSarif(failed));
  const directory = mkdtempSync(join(tmpdir(), 'sarif-test-'));
  try {
    assert.throws(() => checkDirectory(directory));
    writeFileSync(join(directory, 'clean.sarif'), JSON.stringify(sarif('4')));
    checkDirectory(directory);
    writeFileSync(join(directory, 'critical.sarif'), JSON.stringify(sarif('9')));
    assert.throws(() => checkDirectory(directory));
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test('CodeQL CLI reports all analysis errors and findings with their SARIF file and location', () => {
  const directory = mkdtempSync(join(tmpdir(), 'codeql-diagnostics-'));
  try {
    const report = sarif('4');
    report.runs[0].artifacts = [{ location: { uri: 'backend/Example.cs' } }];
    report.runs[0].invocations = [{
      executionSuccessful: false,
      exitCode: 2,
      exitCodeDescription: 'Extraction failed',
      toolExecutionNotifications: [{
        level: 'error',
        descriptor: { id: 'extractor/error' },
        message: { text: 'Could not extract source: 100%\nMore detail' },
        locations: [{ physicalLocation: { artifactLocation: { index: 0 }, region: { startLine: 12, startColumn: 3 } } }],
        exception: { kind: 'ExtractionException', message: 'Missing dependency' },
      }],
      toolConfigurationNotifications: [{ level: 'error', message: { markdown: 'Query pack unavailable' } }],
    }];
    writeFileSync(join(directory, 'csharp.sarif'), JSON.stringify(report));
    const finding = sarif('9');
    finding.runs[0].results[0].message = { text: 'Unsafe input' };
    finding.runs[0].results[0].locations = [{ physicalLocation: { artifactLocation: { uri: 'frontend/example.ts' }, region: { startLine: 7 } } }];
    writeFileSync(join(directory, 'javascript.sarif'), JSON.stringify(finding));
    const result = spawnSync(process.execPath, ['.github/scripts/check-codeql.mjs', directory], {
      encoding: 'utf8', env: { ...process.env, GITHUB_ACTIONS: 'true' },
    });
    assert.equal(result.status, 1);
    for (const detail of [
      '::error title=CodeQL release gate::', 'csharp.sarif', 'exit code 2', 'Extraction failed',
      'extractor/error', '100%25%0AMore detail', 'backend/Example.cs:12:3', 'Missing dependency',
      'Query pack unavailable', 'javascript.sarif', 'security-severity=9', 'Unsafe input', 'frontend/example.ts:7',
    ]) assert.ok(result.stderr.includes(detail), `Missing diagnostic: ${detail}`);
    assert.ok(!result.stderr.includes('at checkSarif'));
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
