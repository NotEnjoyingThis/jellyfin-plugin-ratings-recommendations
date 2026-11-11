// Tests for scripts/release.mjs: node --test scripts/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { addVersions, extractNotes, readBuilds, versionFromTag, zipName } from './release.mjs';

const changelog = `# Changelog

## [Unreleased]

## [1.1.0] - 2027-01-10
### Fixed
- Something.

## [1.0.0] - 2026-12-01
### Added
- First release.
`;

const builds = [
    { id: 'jf10.11', targetAbi: '10.11.9.0' },
    { id: 'jf12', targetAbi: '12.0.0.0' },
];

function manifest() {
    return [{ guid: 'g', name: 'Ratings & Recommendations', versions: [] }];
}

function checksums(version) {
    return {
        [zipName(version, 'jf10.11')]: 'a'.repeat(32),
        [zipName(version, 'jf12')]: 'b'.repeat(32),
    };
}

test('a tag vX.Y.Z gives plugin version X.Y.Z.0', () => {
    assert.deepEqual(versionFromTag('v1.2.3'), { semver: '1.2.3', plugin: '1.2.3.0' });
    assert.throws(() => versionFromTag('1.2.3'));
    assert.throws(() => versionFromTag('v1.2'));
});

test('release notes are the section of the version', () => {
    assert.equal(extractNotes(changelog, '1.1.0'), '### Fixed\n- Something.');
    assert.equal(extractNotes(changelog, '1.0.0'), '### Added\n- First release.');
    assert.throws(() => extractNotes(changelog, '2.0.0'), /no "## \[2.0.0\]" section/);
});

test('builds and target ABIs come from build.yaml', () => {
    assert.deepEqual(readBuilds(readFileSync(new URL('../build.yaml', import.meta.url), 'utf8')), builds.map((b) => ({ ...b })));
});

test('new versions go on top, Jellyfin 12 first, old versions are kept', () => {
    const m = manifest();
    addVersions(m, { tag: 'v1.0.0', repo: 'o/r', timestamp: 't1', changelog: 'c1', builds, checksums: checksums('1.0.0.0') });
    addVersions(m, { tag: 'v1.0.1', repo: 'o/r', timestamp: 't2', changelog: 'c2', builds, checksums: checksums('1.0.1.0') });

    assert.deepEqual(m[0].versions.map((v) => `${v.version} ${v.targetAbi}`), [
        '1.0.1.0 12.0.0.0',
        '1.0.1.0 10.11.9.0',
        '1.0.0.0 12.0.0.0',
        '1.0.0.0 10.11.9.0',
    ]);
    const first = m[0].versions[0];
    assert.equal(first.sourceUrl, 'https://github.com/o/r/releases/download/v1.0.1/ratings-recommendations_1.0.1.0_jf12.zip');
    assert.equal(first.checksum, 'b'.repeat(32));
    assert.equal(first.changelog, 'c2');
    assert.equal(first.timestamp, 't2');
});

test('the same version cannot be added twice, and checksums are required', () => {
    const m = manifest();
    addVersions(m, { tag: 'v1.0.0', repo: 'o/r', timestamp: 't', changelog: 'c', builds, checksums: checksums('1.0.0.0') });
    assert.throws(() => addVersions(m, { tag: 'v1.0.0', repo: 'o/r', timestamp: 't', changelog: 'c', builds, checksums: checksums('1.0.0.0') }), /already has/);
    assert.throws(() => addVersions(manifest(), { tag: 'v2.0.0', repo: 'o/r', timestamp: 't', changelog: 'c', builds, checksums: {} }), /MD5/);
});
