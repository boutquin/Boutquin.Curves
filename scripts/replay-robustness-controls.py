#!/usr/bin/env python3
"""Replay prior Curves faults on an isolated source tree; require assertion-red/restored-green."""
import argparse
import ast
import hashlib
import json
import os
from pathlib import Path
import re
import runpy
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
HISTORY = ROOT / 'specs/verification/rc-2026-10-27'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True, type=Path)
    parser.add_argument('--packages', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    root, output = args.root.resolve(), args.output.resolve()
    assert root != ROOT, 'Mutations require an isolated checkout'
    assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=root).strip()
    source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    output.mkdir(parents=True, exist_ok=False)
    env = dict(os.environ, NUGET_PACKAGES=str(args.packages.resolve()))
    cases = []
    for name, relative, old, new, module, test in runpy.run_path(str(HISTORY / 'numerical/run-mutations.py'))['MUTATIONS']:
        path = 'src/Boutquin.Curves.' + relative
        def transform(p, text, old=old, new=new):
            assert text.count(old) == 1
            return text.replace(old, new)
        cases.append((name, module, 'FullyQualifiedName~' + test, [path], transform))
    def collect(name, module, test, paths, transform=None):
        cases.append((name, module, test, paths, transform))
    tree = ast.parse((HISTORY / 'composition/run-controls.py').read_text())
    selected = [n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == 'remove_defined_guard'
                or isinstance(n, ast.Expr) and isinstance(n.value, ast.Call) and isinstance(n.value.func, ast.Name) and n.value.func.id == 'execute']
    exec(compile(ast.Module(body=selected, type_ignores=[]), '<historical-controls>', 'exec'), {'execute': collect, 're': re})
    fault = json.loads((HISTORY / 'zero-coupon/helper-mutation/report.json').read_text())['mutation']
    def zero(p, text):
        assert text.count(fault['original']) == 1
        return text.replace(fault['original'], fault['replacement'])
    cases.append(('continuous-zero', 'Bootstrap', 'FullyQualifiedName~ContinuousZeroInstrumentHelperTests', [fault['path']], zero))
    assert len(cases) == 29
    results = []
    def execute(module, test, target, label):
        command = ['dotnet', 'test', f'tests/Boutquin.Curves.{module}.Tests/Boutquin.Curves.{module}.Tests.csproj', '-c', 'Release', '--no-restore', '-m:1', '--disable-build-servers', '-p:UseSharedCompilation=false', '--filter', test, '--logger', f'trx;LogFileName={label}.trx', '--results-directory', str(target)]
        with (target / (label + '.txt')).open('x') as log:
            result = subprocess.run(command, cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT)
        trx = target / (label + '.trx')
        assert trx.exists(), 'Compiler-only failure does not count: ' + str(target)
        counters = ET.parse(trx).find('.//{*}Counters').attrib
        assert int(counters['executed']) > 0
        return result.returncode, counters
    for name, module, test, paths, transform in cases:
        target = output / name; target.mkdir()
        originals = {p: (root / p).read_bytes() for p in paths}
        try:
            for p, data in originals.items():
                changed = subprocess.check_output(['git', 'show', 'd99636557d437590cd98bc4b15fe7b435a3fc06b:' + p], cwd=root) if transform is None else transform(p, data.decode()).encode()
                assert changed != data
                (root / p).write_bytes(changed)
            exit_code, red = execute(module, test, target, 'red')
            assert exit_code != 0 and int(red['failed']) > 0, name
        finally:
            for p, data in originals.items():
                (root / p).write_bytes(data)
                assert (root / p).read_bytes() == data
        exit_code, green = execute(module, test, target, 'green')
        assert exit_code == 0 and int(green['failed']) == int(green['notExecuted']) == 0, name
        results.append(dict(name=name, sourceCommit=source, red=red, green=green, restored=True, sourceHashes={p: hashlib.sha256(d).hexdigest() for p,d in originals.items()}))
        (output / 'results.json').write_text(json.dumps(results, indent=2) + '\n')
        print(name, 'assertion-red/restored-green', flush=True)
    assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=root).strip()
    (output / 'summary.json').write_text(json.dumps(dict(status='PASS', sourceCommit=source, assertionRejectedAndRestoredGreen=len(results), worktreeClean=True), indent=2) + '\n')


if __name__ == '__main__':
    main()
