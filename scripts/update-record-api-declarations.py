#!/usr/bin/env python3
"""Run RS0016 declaration fix in dependency order, preserving every build result."""
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'specs/verification/dependency-refresh-2026-10-30/api-compatibility'

def main():
    OUT.mkdir(exist_ok=False)
    env = dict(os.environ, NUGET_PACKAGES=str(ROOT/'artifacts/dependency-refresh-20261030/direct-stable/packages'))
    before = {p:p.read_bytes() for p in ROOT.glob('src/**/*.cs')}
    records = []
    for stage in range(1, 9):
        for name, argv in [
            ('fix', ['dotnet','format','analyzers','Boutquin.Curves.slnx','--diagnostics','RS0016','--no-restore','--verbosity','minimal']),
            ('build', ['dotnet','build','Boutquin.Curves.slnx','-c','Release','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-p:GeneratePackageOnBuild=false'])]:
            result = subprocess.run(argv,cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
            (OUT/f'{stage}-{name}.txt').write_bytes(result.stdout)
            records.append({'stage':stage,'name':name,'argv':argv,'exit':result.returncode})
            (OUT/'commands.json').write_text(json.dumps(records,indent=2)+'\n')
            print(stage,name,result.returncode,flush=True)
            assert all(p.read_bytes()==b for p,b in before.items() if 'obj' not in p.parts), 'Unexpected implementation edit'
            if name=='fix' and result.returncode:
                raise SystemExit('Formatter failed')
        if result.returncode==0:
            return
        errors=set(re.findall(rb': error (\w+):',result.stdout))
        if errors != {b'RS0016'}:
            raise SystemExit(f'Non-declaration build errors require diagnosis: {errors}')
    raise SystemExit('Declaration graph exceeded bounded repair passes')

if __name__=='__main__':
    main()
