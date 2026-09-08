"""Read-only repository checks. Not a substitute for Unity compilation/Play Mode.

Run from any directory: python Tools/validate_multiplayer.py
Uses only Python's standard library.
"""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def validate():
    guids = {}
    for path in (ROOT / "Assets").rglob("*.meta"):
        match = re.search(r"^guid: (\w+)", path.read_text(encoding="utf-8-sig"), re.M)
        if match:
            guid = match[1]
            assert guid not in guids, f"Duplicate GUID: {path}, {guids.get(guid)}"
            guids[guid] = path.with_suffix("")
    for path in (ROOT / "Assets").rglob("*"):
        if path.suffix in {".cs", ".shader", ".prefab", ".unity"}:
            assert not re.search(r"^(<<<<<<< |=======\s*$|>>>>>>> )", path.read_text(encoding="utf-8-sig"), re.M), path

    manifest = json.loads(read("Packages/manifest.json"))["dependencies"]
    lock = json.loads(read("Packages/packages-lock.json"))["dependencies"]
    assert manifest["com.unity.netcode.gameobjects"] == lock["com.unity.netcode.gameobjects"]["version"]
    assert manifest["com.unity.services.multiplayer"] == "2.3.1"
    online_transport = read("Assets/Scripts/Net/OnlineSessionTransport.cs")
    for required in ["IOnlineSessionTransport", "CreateAllocationAsync", "JoinAllocationAsync", "dtls"]:
        assert required in online_transport, required
    connection = read("Assets/Scripts/Net/ConnectionManager.cs")
    for required in ["InternetRelay", "StartRelayHost", "StartRelayClient", "ProtocolVersion = 3"]:
        assert required in connection, required

    prefabs = [ROOT / "Assets/Prefabs/Book.prefab"] + sorted((ROOT / "Assets/Prefabs/VeridianBooks").glob("*.prefab"))
    assert len(prefabs) == 16, "Expected fallback + 15 authored books"
    hashes = set()
    registered = set(re.findall(r"guid: ([a-f0-9]{32})", read("Assets/DefaultNetworkPrefabs.asset")))
    for path in prefabs:
        data = path.read_text(encoding="utf-8-sig")
        assert "Assembly-CSharp::NetworkBook" in data, path
        assert "Unity.Netcode.NetworkObject" in data, path
        assert "AutoObjectParentSync: 0" in data, path
        assert "AlwaysReplicateAsRoot: 1" in data, path
        match = re.search(r"GlobalObjectIdHash: (\d+)", data)
        assert match and int(match[1]) != 0, path
        assert match[1] not in hashes, f"Duplicate network hash: {path}"
        hashes.add(match[1])
        guid = re.search(r"guid: (\w+)", Path(str(path) + ".meta").read_text())[1]
        assert guid in registered, f"Unregistered book prefab: {path}"

    player = read("Assets/Prefabs/Player.prefab")
    for component in ["NetworkPlayerSetup", "ClientNetworkTransform", "PlayerController", "PlayerInteraction"]:
        assert "Assembly-CSharp::" + component in player, component
    scene = read("Assets/Settings/ne.unity")
    for component in ["ConnectionManager", "BookSpawner"]:
        assert "Assembly-CSharp::" + component in scene, component
    for field in ["columnNeighbor", "rowNeighbor"]:
        assert re.search(field + r": \{fileID: (?!0\})-?\d+\}", scene), field
    assert "Assets/Settings/ne.unity" in read("ProjectSettings/EditorBuildSettings.asset")

    # All project-owned MonoBehaviour script references must resolve. Package-owned
    # script references are validated by Unity after Package Manager import.
    for path in [*prefabs, ROOT / "Assets/Prefabs/Player.prefab", ROOT / "Assets/Settings/ne.unity"]:
        for guid, component in re.findall(
            r"m_Script: \{[^\n]*guid: (\w+)[^\n]*\}\s*\n\s*m_Name:[^\n]*\n\s*m_EditorClassIdentifier: Assembly-CSharp::(\w+)",
            path.read_text(encoding="utf-8-sig"),
        ):
            assert guid in guids and guids[guid].exists(), (path, component, guid)
    print(f"PASS: {len(guids)} unique asset GUIDs; 16 registered book prefabs; player/scene wiring; package versions; no merge markers.")
    print("NOT RUN: C# compilation, NGO code generation, host/client transport, Unity Play Mode.")


if __name__ == "__main__":
    validate()
