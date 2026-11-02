#!/usr/bin/env python3
"""Qualify frozen Curves packages with all committed tests and the offline example."""
from __future__ import annotations

import argparse
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
import zipfile
import urllib.request
import runpy

ROOT = Path(__file__).resolve().parents[1]
SOLUTION = "Boutquin.Curves.slnx"
IDS = tuple("Boutquin.Curves." + name for name in (
    "Abstractions", "Conventions", "Core", "Interpolation", "Bootstrap",
    "Recipes", "Risk", "Serialization", "Indices", "Quotes"))
EXAMPLES = "Boutquin.Curves.Examples"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def run(command, cwd, output, env):
    with output.open("x", encoding="utf-8") as log:
        result = subprocess.run(command, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=1800)
    require(result.returncode == 0, f"Exit {result.returncode}: {command}; see {output}")


def verify_hashes(feed, expected):
    actual = {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
              for path in feed.iterdir() if path.suffix in (".nupkg", ".snupkg")}
    require(actual == expected, "Candidate archive inventory or hashes changed")


def prepare(consumer, version, commit):
    """Copy committed fixtures verbatim, with no production library source."""
    consumer.mkdir()
    content = subprocess.check_output([
        "git", "archive", "--format=zip", commit, "tests", "src/" + EXAMPLES,
        "Directory.Build.props", "Directory.Packages.props", "global.json", ".editorconfig", "README.md"] , cwd=ROOT)
    with zipfile.ZipFile(io.BytesIO(content)) as archive:
        for entry in archive.infolist():
            path = Path(entry.filename)
            require(not path.is_absolute() and ".." not in path.parts, "Unsafe source archive path")
            require(not {"bin", "obj", "TestResults"}.intersection(path.parts), "Generated files tracked in source")
            if not entry.is_dir() and path.name != "packages.lock.json":
                target = consumer / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.read(entry))
    shutil.move(consumer / "src" / EXAMPLES, consumer / "consumer-app")
    (consumer / "src").rmdir()
    projects = sorted((consumer / "tests").rglob("*.csproj"))
    require(len(projects) == 13, "Expected all 13 test projects")
    solution = ET.Element("Solution")
    folder = ET.SubElement(solution, "Folder", Name="/tests/")
    for project in projects:
        ET.SubElement(folder, "Project", Path=project.relative_to(consumer).as_posix())
    folder = ET.SubElement(solution, "Folder", Name="/consumer-app/")
    ET.SubElement(folder, "Project", Path=f"consumer-app/{EXAMPLES}.csproj")
    ET.ElementTree(solution).write(consumer / SOLUTION, encoding="unicode")
    ids = set()
    app_references = 0
    for project in [*projects, consumer / "consumer-app" / f"{EXAMPLES}.csproj"]:
        tree = ET.parse(project)
        for reference in tree.findall(".//ProjectReference"):
            name = Path(reference.get("Include").replace("\\", "/")).stem
            if name == EXAMPLES:
                require(project.parent.name == "Boutquin.Curves.ArchitectureTests", "Unexpected local app reference")
                reference.set("Include", f"../../consumer-app/{EXAMPLES}.csproj")
                app_references += 1
            else:
                require(name in IDS, f"Unknown production reference: {name}")
                ids.add(name)
                reference.tag = "PackageReference"
                reference.attrib.clear()
                reference.set("Include", name)
                reference.set("VersionOverride", f"[{version}]")
        tree.write(project, encoding="unicode")
    require(ids == set(IDS), "Consumer must reference every Curves package")
    require(app_references == 1, "Expected the architecture test's single copied Examples reference")
    return projects


def expected_packages(feed, inspection, locked, public_archives):
    expected = {}
    require(not locked.get("packages"), "Private feed packages are not permitted")
    for item in locked["publishedPackages"]:
        archive = public_archives / item["file"]
        url = "https://api.nuget.org/v3-flatcontainer/" + item["id"].lower() + "/" + item["version"].lower() + "/" + item["file"].lower()
        urllib.request.urlretrieve(url, archive)
        checker = runpy.run_path(str(ROOT / "scripts/check-runner-dependencies.py"))
        checker["verify_archive"](item, archive)
        expected[item["id"]] = dict(item, archive=archive)
    require({item["id"] for item in inspection["packages"]} == set(IDS), "Incomplete candidate inspection")
    for item in inspection["packages"]:
        expected[item["id"]] = dict(item, version=inspection["version"], sourceCommit=inspection["sourceCommit"],
            archive=feed / f"{item['id']}.{inspection['version']}.nupkg")
    return expected


def verify_restore(consumer, cache, expected):
    """Check exact versions, source, full archive bytes and all restored DLL bytes."""
    resolved = set()
    for project in consumer.rglob("*.csproj"):
        assets = json.loads((project.parent / "obj/project.assets.json").read_text())
        require({Path(path).resolve() for path in assets["packageFolders"]} == {cache.resolve()},
                f"Non-isolated package cache: {project}")
        for identity, library in assets["libraries"].items():
            name, version = identity.rsplit("/", 1)
            if library["type"] == "project":
                require(name == EXAMPLES and project.parent.name == "Boutquin.Curves.ArchitectureTests",
                        f"Production project reference leaked into consumer: {identity}")
                continue
            if not name.startswith("Boutquin."):
                continue
            require(name in expected, f"Unpinned private package {identity}")
            package = expected[name]
            require(version == package["version"], f"Wrong private package version {identity}")
            folder = cache / name.lower() / version.lower()
            metadata = json.loads((folder / ".nupkg.metadata").read_text())
            archive = package["archive"]
            require(metadata["source"] == package["packageSource"] if "packageSource" in package else Path(metadata["source"]).resolve() == archive.parent.resolve(), f"Wrong source {identity}")
            content = archive.read_bytes()
            require(metadata["contentHash"] == package.get("contentHash", base64.b64encode(hashlib.sha512(content).digest()).decode()), f"Wrong resolved hash {identity}")
            require((folder / f"{name.lower()}.{version.lower()}.nupkg").read_bytes() == content, f"Wrong cached bytes {identity}")
            with zipfile.ZipFile(archive) as zipped:
                for entry in zipped.namelist():
                    if entry.endswith(".dll"):
                        require((folder / entry).read_bytes() == zipped.read(entry), f"Wrong restored DLL {identity}:{entry}")
            resolved.add(name)
    require(set(expected) <= resolved, f"Some qualified packages did not resolve: {sorted(set(expected) - resolved)}")
    return sorted(resolved)


def write_identity_probe(consumer, expected):
    """Load every candidate and upstream runtime DLL from a package-only executable."""
    directory = consumer / "identity-probe"
    directory.mkdir()
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    props = ET.SubElement(project, "PropertyGroup")
    for name, value in (("OutputType", "Exe"), ("IsPackable", "false")):
        ET.SubElement(props, name).text = value
    group = ET.SubElement(project, "ItemGroup")
    runtime = []
    for name, package in expected.items():
        ET.SubElement(group, "PackageReference", Include=name, VersionOverride=f"[{package['version']}]")
        with zipfile.ZipFile(package["archive"]) as zipped:
            dll = f"lib/net10.0/{name}.dll"
            if dll not in zipped.namelist():
                require(name == "Boutquin.Storage.SourceGenerator", f"Unexpected qualified runtime layout: {name}")
                continue
            core = package["version"].split("-")[0]
            runtime.append(dict(id=name, informationalVersion=package["version"] + "+" + package["sourceCommit"],
                assemblyVersion=core.split(".")[0] + ".0.0.0", fileVersion=core + ".0",
                dllSha256=hashlib.sha256(zipped.read(dll)).hexdigest()))
    ET.SubElement(group, "None", Include="runtime-identities.json", CopyToOutputDirectory="PreserveNewest")
    ET.ElementTree(project).write(directory / "IdentityProbe.csproj", encoding="unicode")
    (directory / "runtime-identities.json").write_text(json.dumps(runtime, indent=2) + "\n")
    header = (consumer / 'consumer-app/Program.cs').read_text().split('using ')[0]
    (directory / "Program.cs").write_text(header + '''using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "runtime-identities.json")));
var results = new List<object>();
foreach (var package in document.RootElement.EnumerateArray())
{
    var name = package.GetProperty("id").GetString()!;
    var assembly = Assembly.Load(name);
    var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant();
    if (assembly.GetName().Name != name || assembly.GetName().Version?.ToString() != package.GetProperty("assemblyVersion").GetString()
        || System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion != package.GetProperty("fileVersion").GetString()
        || informationalVersion != package.GetProperty("informationalVersion").GetString()
        || hash != package.GetProperty("dllSha256").GetString())
    {
        throw new InvalidOperationException($"Loaded package differs from frozen bytes: {name}");
    }
    results.Add(new { name, assemblyVersion = assembly.GetName().Version!.ToString(), fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion, informationalVersion, dllSha256 = hash });
}
Console.WriteLine(JsonSerializer.Serialize(results));
''')
    return directory / "IdentityProbe.csproj", len(runtime)


def write_config(consumer, feed, locked):
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    for key, value in (("candidate", str(feed)), ("nuget.org", "https://api.nuget.org/v3/index.json")):
        ET.SubElement(sources, "add", key=key, value=value)
    mapping = ET.SubElement(config, "packageSourceMapping")
    for key, patterns in (("candidate", IDS), ("nuget.org", ["*"])):
        source = ET.SubElement(mapping, "packageSource", key=key)
        for pattern in patterns:
            ET.SubElement(source, "package", pattern=pattern)
    ET.ElementTree(config).write(consumer / "nuget.config", encoding="unicode")


def write_readme_examples(consumer, version):
    snippets = re.findall(r"```csharp\n(.*?)```", (consumer / "README.md").read_text(), re.S)
    require(len(snippets) == 3, "Expected fixture, network and flat-curve README examples")
    projects = []
    for index, snippet in enumerate(snippets):
        directory = consumer / f"readme-example-{index}"
        directory.mkdir()
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        props = ET.SubElement(project, "PropertyGroup")
        for name, value in (("OutputType", "Exe"), ("IsPackable", "false")):
            ET.SubElement(props, name).text = value
        group = ET.SubElement(project, "ItemGroup")
        for name in IDS:
            ET.SubElement(group, "PackageReference", Include=name, VersionOverride=f"[{version}]")
        ET.SubElement(group, "PackageReference", Include="Microsoft.Extensions.DependencyInjection")
        ET.SubElement(group, "PackageReference", Include="Microsoft.Extensions.Logging")
        if index == 1:
            ET.SubElement(group, "PackageReference", Include="Boutquin.MarketData.DependencyInjection")
        path = directory / f"ReadmeExample{index}.csproj"
        ET.ElementTree(project).write(path, encoding="unicode")
        header = (consumer / 'consumer-app/Program.cs').read_text().split('using ')[0]
        (directory / "Program.cs").write_text(header + snippet)
        projects.append(path)
    return projects


def test_counters(directory, projects):
    counters = []
    for trx in directory.glob("*.trx"):
        tree = ET.parse(trx)
        count = tree.find(".//{*}Counters").attrib
        require(int(count["failed"]) == 0 and int(count["notExecuted"]) == 0 and int(count["total"]) > 0
                and int(count["passed"]) == int(count["total"]), f"Non-green test lane: {trx}")
        assemblies = {Path(item.get("storage").replace("\\", "/")).stem.lower()
                      for item in tree.findall(".//{*}TestDefinitions/{*}UnitTest")}
        require(len(assemblies) == 1, f"Expected one test assembly per result: {trx}")
        counters.append(dict(file=trx.name, assembly=assemblies.pop(), **count))
    require(len(counters) == len(projects), "Expected results for all 13 test projects")
    require(all(sum(project.stem.lower() == item["assembly"] for item in counters) == 1 for project in projects),
            "Missing or duplicate test project result")
    return counters


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("feed", "inspection", "manifest", "work", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    feed, work, output = args.feed.resolve(), args.work.resolve(), args.output.resolve()
    inspection = json.loads(args.inspection.read_text())
    manifest = json.loads(args.manifest.read_text())
    require(inspection["status"] == "PASS", "Package inspection did not pass")
    require({key: inspection[key] for key in ("version", "sourceCommit", "sha256")} == manifest, "Inspection differs from original manifest")
    require(subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip() == inspection["sourceCommit"],
            "Consumer must execute at frozen source HEAD")
    verify_hashes(feed, manifest["sha256"])
    locked = json.loads(subprocess.check_output(["git", "show", inspection["sourceCommit"] + ":dependency-source.json"], cwd=ROOT))
    work.mkdir(parents=True, exist_ok=False)
    output.mkdir(parents=True, exist_ok=False)
    consumer = work / "consumer"
    projects = prepare(consumer, inspection["version"], inspection["sourceCommit"])
    public_archives = work / "published-archives"
    public_archives.mkdir()
    expected = expected_packages(feed, inspection, locked, public_archives)
    probe, runtime_count = write_identity_probe(consumer, expected)
    readme_projects = write_readme_examples(consumer, inspection["version"])
    solution = ET.parse(consumer / SOLUTION)
    for project in [probe, *readme_projects]:
        ET.SubElement(solution.getroot(), "Project", Path=project.relative_to(consumer).as_posix())
    solution.write(consumer / SOLUTION, encoding="unicode")
    write_config(consumer, feed, locked)
    cache, http = work / "packages", work / "http-cache"
    cache.mkdir(); http.mkdir()
    env = dict(os.environ, NUGET_PACKAGES=str(cache), NUGET_HTTP_CACHE_PATH=str(http))
    run(["python3", str(ROOT / "scripts/check-runner-dependencies.py")], ROOT, output / "dependency-bytes.log", env)
    run(["dotnet", "restore", SOLUTION, "--configfile", "nuget.config", "--no-http-cache", "-p:RestorePackagesWithLockFile=true", "-p:_WorkloadLibraryPacksFolder=" + str(work / "absent-sdk-feed")], consumer, output / "restore.log", env)
    resolved = verify_restore(consumer, cache, expected)
    run(["dotnet", "restore", SOLUTION, "--configfile", "nuget.config", "--no-http-cache", "--locked-mode", "-p:_WorkloadLibraryPacksFolder=" + str(work / "absent-sdk-feed")], consumer, output / "locked-restore.log", env)
    run(["dotnet", "build", SOLUTION, "-c", "Release", "--no-restore"], consumer, output / "build.log", env)
    run(["dotnet", "test", SOLUTION, "-c", "Release", "--no-restore", "--no-build", "--logger", "trx", "--results-directory", str(output / "trx")], consumer, output / "tests.log", env)
    run(["dotnet", "run", "--project", str(probe), "-c", "Release", "--no-build", "--no-restore"], consumer, output / "runtime-identities.json", env)
    run(["python3", str(ROOT / "scripts/verify-dependency-identities.py"), "--packages", str(cache), "--outputs", str(consumer), "--report", str(output / "dependency-identities.json")], ROOT, output / "dependency-identities.txt", env)
    identities = json.loads((output / "runtime-identities.json").read_text())
    require(len(identities) == runtime_count, "Not every runtime package loaded")
    run(["dotnet", "run", "--project", f"consumer-app/{EXAMPLES}.csproj", "-c", "Release", "--no-build", "--no-restore"], consumer, output / "example.log", env)
    example = (output / "example.log").read_text()
    require(all(label in example for label in ("USD SOFR Discount", "CAD CORRA Discount", "GBP SONIA Discount", "EUR ESTR Discount", "Done.")), "Offline example did not build all four curves")
    require(len(re.findall(r"DF\((?:spot|1Y|5Y|10Y)\)\s*=\s*([0-9.]+)", example)) == 16, "Offline example has missing or invalid discount factors")
    for index in (0, 2):
        run(["dotnet", "run", "--project", str(readme_projects[index]), "-c", "Release", "--no-build", "--no-restore"], consumer, output / f"readme-example-{index}.log", env)
    require(re.search(r"2Y DF:\s*[0-9.]+", (output / "readme-example-0.log").read_text()), "README fixture did not query its curve")
    verify_restore(consumer, cache, expected)
    verify_hashes(feed, manifest["sha256"])
    require(json.loads(args.manifest.read_text()) == manifest, "Manifest changed during qualification")
    counters = test_counters(output / "trx", projects)
    for lock in consumer.rglob("packages.lock.json"):
        target = output / "locks" / lock.relative_to(consumer)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(lock, target)
    report = dict(status="PASS", sourceCommit=inspection["sourceCommit"], version=inspection["version"],
        resolvedPrivatePackages=resolved, counters=counters, runtimeIdentities=identities,
        candidateHashesUnchanged=True, productionLibraryProjectReferences=0,
        copiedExampleProjectReferences=1, offlineExampleCurrencies=["USD", "CAD", "GBP", "EUR"],
        readmeExamples={"fixture": "executed", "flatCurve": "executed", "network": "compiled"})
    with (output / "report.json").open("x") as stream:
        json.dump(report, stream, indent=2); stream.write("\n")
    print(f"PASS: cold package-only consumer, {sum(int(c['total']) for c in counters)} tests, {runtime_count} runtime DLLs, four offline curves")


if __name__ == "__main__":
    main()
