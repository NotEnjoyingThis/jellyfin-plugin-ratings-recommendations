#!/usr/bin/env node
// Release helpers used by .github/workflows/release.yml (Node only, no dependencies).
//
//   node scripts/release.mjs notes <X.Y.Z>
//       Prints the CHANGELOG.md section of the version (fails if there is none).
//   node scripts/release.mjs manifest --tag vX.Y.Z --repo owner/name --timestamp <ISO> [--artifacts artifacts] [--manifest manifest.json]
//       Adds the new versions at the top of manifest.json: one entry per build of build.yaml, Jellyfin 12 first
//       (highest targetAbi first), with the MD5 of the zip and the CHANGELOG section as changelog.
//   node scripts/release.mjs verify --tag vX.Y.Z [--manifest manifest.json]
//       Downloads the zips listed in manifest.json for that version and checks their MD5.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

export function versionFromTag(tag) {
    const match = /^v(\d+)\.(\d+)\.(\d+)$/.exec(tag || '');
    if (!match) {
        throw new Error(`Tag must look like vX.Y.Z, got '${tag}'`);
    }

    return { semver: `${match[1]}.${match[2]}.${match[3]}`, plugin: `${match[1]}.${match[2]}.${match[3]}.0` };
}

export function extractNotes(changelog, semver) {
    const lines = changelog.replace(/\r\n/g, '\n').split('\n');
    const start = lines.findIndex((l) => l.startsWith(`## [${semver}]`));
    if (start < 0) {
        throw new Error(`CHANGELOG.md has no "## [${semver}]" section`);
    }

    const rest = lines.slice(start + 1);
    const end = rest.findIndex((l) => l.startsWith('## ['));
    const notes = (end < 0 ? rest : rest.slice(0, end)).join('\n').trim();
    if (!notes) {
        throw new Error(`The CHANGELOG.md section of ${semver} is empty`);
    }

    return notes;
}

export function readBuilds(buildYaml) {
    const builds = [];
    let current = null;
    for (const line of buildYaml.replace(/\r\n/g, '\n').split('\n')) {
        const id = /^\s*-\s*id:\s*"([^"]+)"/.exec(line);
        if (id) {
            current = { id: id[1] };
            builds.push(current);
            continue;
        }

        const abi = /^\s+targetAbi:\s*"([^"]+)"/.exec(line);
        if (abi && current) {
            current.targetAbi = abi[1];
        }
    }

    if (builds.length === 0 || builds.some((b) => !b.targetAbi)) {
        throw new Error('build.yaml: every build needs an id and a targetAbi');
    }

    return builds;
}

function compareVersions(a, b) {
    const pa = a.split('.').map(Number);
    const pb = b.split('.').map(Number);
    for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
        const diff = (pa[i] || 0) - (pb[i] || 0);
        if (diff !== 0) {
            return diff;
        }
    }

    return 0;
}

export function zipName(pluginVersion, buildId) {
    return `ratings-recommendations_${pluginVersion}_${buildId}.zip`;
}

export function addVersions(manifest, { tag, repo, timestamp, changelog, builds, checksums }) {
    const { plugin } = versionFromTag(tag);
    if (!Array.isArray(manifest) || manifest.length !== 1) {
        throw new Error('manifest.json must be an array with exactly one plugin');
    }

    const entry = manifest[0];
    entry.versions = entry.versions || [];
    const added = [...builds]
        .sort((a, b) => compareVersions(b.targetAbi, a.targetAbi))
        .map((build) => {
            if (entry.versions.some((v) => v.version === plugin && v.targetAbi === build.targetAbi)) {
                throw new Error(`manifest.json already has ${plugin} for targetAbi ${build.targetAbi}`);
            }

            const name = zipName(plugin, build.id);
            const checksum = checksums[name];
            if (!/^[0-9a-f]{32}$/.test(checksum || '')) {
                throw new Error(`Missing or invalid MD5 for ${name}`);
            }

            return {
                version: plugin,
                changelog,
                targetAbi: build.targetAbi,
                sourceUrl: `https://github.com/${repo}/releases/download/${tag}/${name}`,
                checksum,
                timestamp,
            };
        });
    entry.versions = [...added, ...entry.versions];
    return manifest;
}

function option(args, name, fallback) {
    const index = args.indexOf(`--${name}`);
    if (index >= 0 && index + 1 < args.length) {
        return args[index + 1];
    }

    if (fallback === undefined) {
        throw new Error(`--${name} is required`);
    }

    return fallback;
}

async function main(argv) {
    const [command, ...args] = argv;
    if (command === 'notes') {
        process.stdout.write(extractNotes(readFileSync('CHANGELOG.md', 'utf8'), args[0]) + '\n');
        return;
    }

    if (command === 'manifest') {
        const tag = option(args, 'tag');
        const { semver, plugin } = versionFromTag(tag);
        const artifacts = option(args, 'artifacts', 'artifacts');
        const manifestPath = option(args, 'manifest', 'manifest.json');
        const builds = readBuilds(readFileSync('build.yaml', 'utf8'));
        const checksums = {};
        for (const build of builds) {
            const name = zipName(plugin, build.id);
            checksums[name] = readFileSync(join(artifacts, `${name}.md5`), 'utf8').trim().split(/\s+/)[0];
        }

        const manifest = addVersions(JSON.parse(readFileSync(manifestPath, 'utf8')), {
            tag,
            repo: option(args, 'repo'),
            timestamp: option(args, 'timestamp'),
            changelog: extractNotes(readFileSync('CHANGELOG.md', 'utf8'), semver),
            builds,
            checksums,
        });
        writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
        console.log(`manifest.json: added ${builds.length} entries for ${plugin}`);
        return;
    }

    if (command === 'verify') {
        const { plugin } = versionFromTag(option(args, 'tag'));
        const manifest = JSON.parse(readFileSync(option(args, 'manifest', 'manifest.json'), 'utf8'));
        const entries = manifest[0].versions.filter((v) => v.version === plugin);
        if (entries.length === 0) {
            throw new Error(`manifest.json has no entry for ${plugin}`);
        }

        for (const v of entries) {
            const response = await fetch(v.sourceUrl);
            if (!response.ok) {
                throw new Error(`${v.sourceUrl}: HTTP ${response.status}`);
            }

            const md5 = createHash('md5').update(Buffer.from(await response.arrayBuffer())).digest('hex');
            if (md5 !== v.checksum) {
                throw new Error(`${v.sourceUrl}: MD5 ${md5} does not match the manifest (${v.checksum})`);
            }

            console.log(`OK ${v.sourceUrl} (${md5})`);
        }

        return;
    }

    throw new Error('Usage: release.mjs notes <X.Y.Z> | manifest --tag vX.Y.Z --repo owner/name --timestamp ISO | verify --tag vX.Y.Z');
}

if (import.meta.url === pathToFileURL(process.argv[1] || '').href) {
    main(process.argv.slice(2)).catch((error) => {
        console.error(error.message);
        process.exit(1);
    });
}
