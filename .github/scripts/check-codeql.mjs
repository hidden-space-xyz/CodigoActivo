import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

function resolveRule(tool, result) {
  const componentReference = result.rule?.toolComponent;
  const component = componentReference
    ? componentReference.index !== undefined
      ? tool.extensions?.[componentReference.index]
      : tool.extensions?.find((item) => item.name === componentReference.name)
    : tool.driver;
  const rules = component?.rules ?? [];
  const id = result.rule?.id ?? result.ruleId;
  const index = result.rule?.index ?? result.ruleIndex;
  const rule = index !== undefined ? rules[index] : rules.find((item) => item.id === id);
  if (!rule || (id !== undefined && rule.id !== id)) {
    throw new Error('Missing or inconsistent CodeQL rule metadata');
  }
  return rule;
}

export function checkSarif(sarif) {
  if (sarif.version !== '2.1.0' || !sarif.runs?.length) throw new Error('Missing SARIF runs');
  const blocked = [];
  for (const run of sarif.runs) {
    if (run.tool?.driver?.name !== 'CodeQL' || !Array.isArray(run.results)) {
      throw new Error('Incomplete CodeQL results');
    }
    for (const invocation of run.invocations ?? []) {
      if (invocation.executionSuccessful === false) throw new Error('CodeQL execution failed');
      for (const notification of [...(invocation.toolExecutionNotifications ?? []), ...(invocation.toolConfigurationNotifications ?? [])]) {
        if (notification.level === 'error') throw new Error('CodeQL reported an analysis error');
      }
    }
    for (const result of run.results) {
      const rule = resolveRule(run.tool, result);
      const level = result.level ?? rule.defaultConfiguration?.level ?? 'warning';
      const severity = rule.properties?.['security-severity'];
      if (severity !== undefined && !Number.isFinite(Number(severity))) throw new Error('Invalid security severity');
      if (level === 'error' || Number(severity) >= 7) blocked.push(result.ruleId ?? rule.id);
    }
  }
  return blocked;
}

export function checkDirectory(directory) {
  const files = readdirSync(directory).filter((file) => file.endsWith('.sarif'));
  if (!files.length) throw new Error('No CodeQL SARIF files found');
  const blocked = files.flatMap((file) => checkSarif(JSON.parse(readFileSync(join(directory, file), 'utf8'))));
  if (blocked.length) throw new Error(`CodeQL blocks release: ${blocked.join(', ')}`);
  console.log(`CodeQL gate passed (${files.length} SARIF files)`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  checkDirectory(process.argv[2]);
}
