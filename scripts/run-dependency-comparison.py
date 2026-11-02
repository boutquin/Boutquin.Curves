#!/usr/bin/env python3
"""Cold audited source gate and exact reproducible numeric snapshot; never overwrite a lane."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('lane')
    p.add_argument('--compare', type=Path)
    p.add_argument('--refresh-locks', action='store_true')
    p.add_argument('--local-chain', action='store_true', help='Explicit isolated local-chain preflight; historical runner source remains separate')
    p.add_argument('--resume-probe', action='store_true', help='Resume failed instrumentation only; production inputs must be unchanged')
    a = p.parse_args()
    out = ROOT/'specs/verification/dependency-refresh-2026-10-30'/a.lane
    out.mkdir(parents=True, exist_ok=a.resume_probe)
    cache = ROOT/'artifacts/dependency-refresh-20261030'/a.lane
    cache.mkdir(parents=True, exist_ok=a.resume_probe)
    env = dict(os.environ, NUGET_PACKAGES=str(cache/'packages'), NUGET_HTTP_CACHE_PATH=str(cache/'http'),
               DOTNET_CLI_TELEMETRY_OPTOUT='1', TZ='UTC')
    receipt = {'sourceCommit': subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(), 'commands': []}
    if a.resume_probe:
        receipt=json.loads((out/'receipt.json').read_text())
        subprocess.run(['git','diff','--exit-code',receipt['sourceCommit'],'--','src','tests','Directory.Build.props','Directory.Packages.props'],cwd=ROOT,check=True)
        assert all(any(c['name']==n and c['exit']==0 for c in receipt['commands']) for n in ('dependencies','restore','build','test'))
    def run(name, argv):
        if a.resume_probe and name in ('dependencies','restore','build','test'):
            return
        log=out/(name+'.txt')
        if log.exists():
            log.rename(out/(name+'.previous.txt'))
        result = subprocess.run(argv,cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
        log.write_bytes(result.stdout)
        receipt['commands'].append({'name':name,'argv':argv,'exit':result.returncode})
        (out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
        print(name,result.returncode,flush=True)
        if result.returncode: raise SystemExit(result.returncode)
    run('dependencies',['python3','scripts/verify-local-chain.py','--require-metadata'] if a.local_chain else ['python3','scripts/check-runner-dependencies.py'])
    run('restore',['dotnet','restore','Boutquin.Curves.slnx','--configfile','nuget.ci.config', '--force-evaluate' if a.refresh_locks else '--locked-mode'])
    if a.local_chain:
        run('restored-identities',['python3','scripts/verify-local-chain.py','--require-metadata','--cache',str(cache/'packages')])
    inputs = subprocess.check_output(['git','ls-files','src','tests','benchmarks','Directory.Packages.props','Directory.Build.props','global.json','nuget.ci.config','dependency-source.json'],cwd=ROOT,text=True).splitlines()
    receipt['inputFileHashes']={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in inputs}
    run('build',['dotnet','build','Boutquin.Curves.slnx','-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-p:GeneratePackageOnBuild=false'])
    run('test',['dotnet','test','Boutquin.Curves.slnx','-c','Release','--no-build','--no-restore','--logger','trx','--results-directory',str(out/'trx')])
    probe='specs/verification/dependency-refresh-2026-10-30/probe/NumericalSnapshot.csproj'
    run('probe-restore',['dotnet','restore',probe,'--configfile','nuget.ci.config'])
    run('probe-build',['dotnet','build',probe,'-c','Release','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-p:GeneratePackageOnBuild=false'])
    dll=ROOT/'specs/verification/dependency-refresh-2026-10-30/probe/bin/Release/net10.0/NumericalSnapshot.dll'
    run('numerical-1',['dotnet',str(dll)])
    run('numerical-2',['dotnet',str(dll)])
    one=(out/'numerical-1.txt').read_bytes();two=(out/'numerical-2.txt').read_bytes()
    assert one==two,'Baseline snapshot not repeatable'
    json.loads(one)
    receipt['numericalSha256']=hashlib.sha256(one).hexdigest()
    receipt['probeSha256']=hashlib.sha256((Path(probe).parent/'Program.cs').read_bytes()).hexdigest()
    receipt['baselineRepeatEqual']=True
    if a.compare:
        receipt['comparisonBaseline']=str(a.compare)
        receipt['numericalExactlyEqual']=one==a.compare.read_bytes()
        if a.local_chain and not receipt['numericalExactlyEqual']:
            (out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
            raise SystemExit('STOP: numerical output delta; affected local chain blocked and escalation required')
    run('offline',['dotnet',str(ROOT/'src/Boutquin.Curves.Examples/bin/Release/net10.0/Boutquin.Curves.Examples.dll')])
    run('format',['dotnet','format','Boutquin.Curves.slnx','--verify-no-changes','--no-restore'])
    run('audit',['dotnet','list','Boutquin.Curves.slnx','package','--vulnerable','--include-transitive','--no-restore','--format','json','--configfile','nuget.ci.config'])
    audit=json.loads((out/'audit.txt').read_text())
    assert all(not f.get('topLevelPackages') and not f.get('transitivePackages') for x in audit['projects'] for f in x.get('frameworks',[]))
    receipt['testCounts']=[ET.parse(x).find('.//{*}Counters').attrib for x in (out/'trx').glob('*.trx')]
    assert receipt['testCounts'] and all(x['passed']==x['total'] and int(x['total'])>0 for x in receipt['testCounts'])
    receipt['runtimeHashes']={str(x.relative_to(ROOT)):hashlib.sha256(x.read_bytes()).hexdigest() for x in (dll.parent).glob('Boutquin.*.dll')}
    assert all(hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest for name,digest in receipt['inputFileHashes'].items()), 'Source inputs changed during verification'
    (out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    if a.compare and not receipt['numericalExactlyEqual']: raise SystemExit('STOP: numerical output delta; affected adoption/publication blocked')
    print(json.dumps({k:v for k,v in receipt.items() if k not in ('commands','runtimeHashes','testCounts','inputFileHashes')}),flush=True)

if __name__=='__main__':main()
