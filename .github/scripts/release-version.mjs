import { execFileSync } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

export function commitBump(subject) {
  const match = /^([a-z]+)(?:\([^\r\n()]+\))?(!)?: .+/.exec(subject);
  if (!match) return 0;
  if (match[2]) return 3;
  if (match[1] === 'feat') return 2;
  return ['fix', 'perf'].includes(match[1]) ? 1 : 0;
}

export function nextVersion(version, bump) {
  const [major, minor, patch] = version.split('.').map(Number);
  if (bump === 3) return `${major + 1}.0.0`;
  if (bump === 2) return `${major}.${minor + 1}.0`;
  if (bump === 1) return `${major}.${minor}.${patch + 1}`;
  return version;
}

export function resolveRelease(component, directory, cwd = process.cwd()) {
  if (!['API', 'UI'].includes(component) || directory !== (component === 'API' ? 'backend' : 'frontend')) {
    throw new Error('Expected API backend or UI frontend');
  }
  const git = (...args) => execFileSync('git', args, { cwd, encoding: 'utf8' }).trim();
  // Keep the existing tag format, including releases made before this pipeline.
  const pattern = new RegExp(`^v(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)-${component}$`);
  const previous = git('tag', '--list', `v*-${component}`, '--sort=-version:refname')
    .split('\n').find((tag) => pattern.test(tag)) ?? '';
  if (previous) {
    // Reject reruns of old commits instead of moving latest backwards.
    git('merge-base', '--is-ancestor', previous, 'HEAD');
  }
  const version = previous ? previous.slice(1, -component.length - 1) : '0.0.0';
  const range = previous ? `${previous}..HEAD` : 'HEAD';
  const commits = git('log', '--format=%H', range).split('\n').filter(Boolean);
  let bump = 0;
  for (const commit of commits) {
    // Merge titles apply to changes introduced into master, relative to the first
    // parent. The log above also includes the merged branch's own commits.
    const files = git('diff-tree', '--root', '--diff-merges=first-parent', '--no-commit-id', '--name-only', '-r', commit, '--', directory);
    if (files) bump = Math.max(bump, commitBump(git('show', '-s', '--format=%s', commit)));
  }
  const next = nextVersion(version, bump);
  return { changed: bump > 0, version: next, tag: `v${next}-${component}`, previous };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [component, directory] = process.argv.slice(2);
  const release = resolveRelease(component, directory);
  const repository = process.env.GITHUB_REPOSITORY;
  if (!repository || !process.env.GITHUB_OUTPUT) throw new Error('GitHub Actions environment required');
  const outputs = { ...release, image: `ghcr.io/${repository.toLowerCase()}-${directory}` };
  appendFileSync(process.env.GITHUB_OUTPUT, Object.entries(outputs).map(([key, value]) => `${key}=${value}\n`).join(''));
  console.log(release.changed ? `Release ${release.tag} (previous: ${release.previous || 'none'})` : `No release for ${component}`);
}
