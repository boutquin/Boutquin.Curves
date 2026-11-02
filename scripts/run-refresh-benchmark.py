#!/usr/bin/env python3
"""Execute an actual BenchmarkDotNet dry job and preserve the command and exit."""
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
lane=sys.argv[1]
out=ROOT/'specs/verification/dependency-refresh-2026-10-30'/lane
log=out/'benchmark.txt'
assert out.is_dir() and not log.exists()
env=dict(os.environ,NUGET_PACKAGES=str(ROOT/'artifacts/dependency-refresh-20261030'/lane/'packages'),
         NUGET_HTTP_CACHE_PATH=str(ROOT/'artifacts/dependency-refresh-20261030'/lane/'http'),
         DOTNET_CLI_TELEMETRY_OPTOUT='1',TZ='UTC')
argv=['dotnet',str(ROOT/'benchmarks/Boutquin.Curves.Benchmarks/bin/Release/net10.0/Boutquin.Curves.Benchmarks.dll'),
      '--job','dry','--filter','*QueryDiscountFactor*','--artifacts',str(out/'benchmark-artifacts')]
result=subprocess.run(argv,cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
log.write_bytes(result.stdout)
(out/'benchmark-command.json').write_text(json.dumps({'argv':argv,'exit':result.returncode},indent=2)+'\n')
print(f'benchmark exit={result.returncode}',flush=True)
raise SystemExit(result.returncode)
