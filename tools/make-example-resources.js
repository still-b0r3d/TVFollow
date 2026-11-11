// Writes examples/example-resources.rsp, the list of the example app's files that build.cmd
// builds into TVFollow, one /resource: line each for the C# compiler. Run it after adding,
// removing or renaming a file in examples/tvfollow-example, once git knows about it ("git add"
// first): it lists the files git tracks there, so a file that's only on this PC can't get into
// a build. It leaves out what TVFollow leaves out of a pushed folder (DevPackage.Skipped).
//   node tools/make-example-resources.js
'use strict';
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const repo = path.resolve(__dirname, '..');
const folder = 'examples/tvfollow-example';
const out = path.join(repo, 'examples', 'example-resources.rsp');

// the same as DevPackage.Skipped: hidden things (.git, .DS_Store), what macOS adds to zips,
// Windows' Thumbs.db and desktop.ini, and editors' temporary and backup files
function skipped(relative) {
    return relative.split('/').some(part => {
        const lower = part.toLowerCase();
        return lower.startsWith('.') || lower.startsWith('~$') || lower.endsWith('~') || lower === '__macosx' ||
            lower === 'thumbs.db' || lower === 'desktop.ini' || lower.endsWith('.tmp') || lower.endsWith('.swp') || lower.endsWith('.bak');
    });
}

const tracked = execFileSync('git', ['-C', repo, 'ls-files', '-z', '--', folder], { encoding: 'utf8' })
    .split('\0').filter(name => name.length > 0).sort();
const lines = [];
for (const name of tracked) {
    const relative = name.substring(folder.length + 1);
    if (skipped(relative)) continue;
    // The compiler reads each line the way it reads a command line. The quotes keep a space
    // from splitting it, but a quote can't go inside them, a comma would end the file's name
    // early, and anything past plain ASCII depends on the code page.
    if (!/^[ -~]+$/.test(name) || name.includes('"') || name.includes(',')) {
        console.error("build.cmd can't take this file name, so rename it: " + name);
        process.exit(1);
    }
    lines.push('"/resource:' + name.split('/').join('\\') + ',example/' + relative + '"');
}
if (lines.length === 0) {
    console.error('git tracks no files in ' + folder);
    process.exit(1);
}
fs.writeFileSync(out, lines.join('\r\n') + '\r\n');
console.log('examples/example-resources.rsp: ' + lines.length + ' files');
