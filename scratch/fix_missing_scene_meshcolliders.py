#!/usr/bin/env python3
import re
from pathlib import Path


SCENE_PATH = Path("/Volumes/HanzoP/Unity/evade/client/mp-demo/Assets/Scenes/Boom Boom.unity")
ASSETS_ROOT = Path("/Volumes/HanzoP/Unity/evade/client/mp-demo/Assets")


def split_blocks(text: str):
    parts = re.split(r"(?=^--- !u!)", text, flags=re.M)
    return [part for part in parts if part.strip()]


def parse_prefab_mesh_lookup(prefab_path: Path):
    text = prefab_path.read_text()
    blocks = split_blocks(text)

    gameobject_to_components = {}
    component_to_gameobject = {}
    mesh_lookup = {}

    for block in blocks:
        if block.startswith("--- !u!1 "):
            gameobject_id_match = re.search(r"^--- !u!1 &([-\d]+)", block, flags=re.M)
            if not gameobject_id_match:
                continue
            gameobject_id = gameobject_id_match.group(1)
            component_ids = re.findall(r"- component: \{fileID: ([-\d]+)\}", block)
            gameobject_to_components[gameobject_id] = component_ids
        else:
            component_id_match = re.search(r"^--- !u!\d+ &([-\d]+)", block, flags=re.M)
            gameobject_id_match = re.search(r"m_GameObject: \{fileID: ([-\d]+)\}", block)
            if component_id_match and gameobject_id_match:
                component_to_gameobject[component_id_match.group(1)] = gameobject_id_match.group(1)

    for block in blocks:
        if not (block.startswith("--- !u!33 ") or block.startswith("--- !u!64 ")):
            continue
        component_id_match = re.search(r"^--- !u!\d+ &([-\d]+)", block, flags=re.M)
        if not component_id_match:
            continue
        component_id = component_id_match.group(1)
        gameobject_id = component_to_gameobject.get(component_id)
        if not gameobject_id:
            continue

        mesh_match = re.search(r"m_Mesh: (\{fileID: [-\d]+, guid: [0-9a-f]+, type: 3\})", block)
        if not mesh_match:
            continue

        current_priority = mesh_lookup.get(gameobject_id, (0, None))[0]
        priority = 2 if block.startswith("--- !u!64 ") else 1
        if priority >= current_priority:
            mesh_lookup[gameobject_id] = (priority, mesh_match.group(1))

    return {gameobject_id: mesh_ref for gameobject_id, (_, mesh_ref) in mesh_lookup.items()}


def build_prefab_index():
    index = {}
    for prefab_path in ASSETS_ROOT.rglob("*.prefab"):
        try:
            prefab_text = prefab_path.read_text()
        except UnicodeDecodeError:
            continue

        guid_match = re.search(r"guid: ([0-9a-f]{32})", prefab_path.with_suffix(prefab_path.suffix + ".meta").read_text())
        if not guid_match:
            continue

        guid = guid_match.group(1)
        mesh_lookup = parse_prefab_mesh_lookup(prefab_path)
        if mesh_lookup:
            index[guid] = mesh_lookup

    return index


def main():
    scene_text = SCENE_PATH.read_text()
    prefab_index = build_prefab_index()
    blocks = split_blocks(scene_text)

    scene_gameobject_sources = {}
    for block in blocks:
        if not block.startswith("--- !u!1 "):
            continue

        scene_go_match = re.search(r"^--- !u!1 &([-\d]+)", block, flags=re.M)
        source_match = re.search(
            r"m_CorrespondingSourceObject: \{fileID: ([-\d]+), guid: ([0-9a-f]{32}), type: 3\}",
            block,
        )
        if scene_go_match and source_match:
            scene_gameobject_sources[scene_go_match.group(1)] = (source_match.group(1), source_match.group(2))

    updated_blocks = []
    fixed_count = 0

    for block in blocks:
        if not block.startswith("--- !u!64 "):
            updated_blocks.append(block)
            continue

        if "m_Mesh: {fileID: 0}" not in block:
            updated_blocks.append(block)
            continue

        gameobject_match = re.search(r"m_GameObject: \{fileID: ([-\d]+)\}", block)
        if not gameobject_match:
            updated_blocks.append(block)
            continue

        scene_gameobject_id = gameobject_match.group(1)
        source_info = scene_gameobject_sources.get(scene_gameobject_id)
        if not source_info:
            updated_blocks.append(block)
            continue

        source_gameobject_id, prefab_guid = source_info
        mesh_lookup = prefab_index.get(prefab_guid)
        mesh_ref = mesh_lookup.get(source_gameobject_id) if mesh_lookup else None
        if not mesh_ref:
            updated_blocks.append(block)
            continue

        updated_blocks.append(block.replace("m_Mesh: {fileID: 0}", f"m_Mesh: {mesh_ref}", 1))
        fixed_count += 1

    if fixed_count == 0:
        print("No missing MeshCollider meshes could be fixed.")
        return

    SCENE_PATH.write_text("".join(updated_blocks))
    print(f"Fixed {fixed_count} missing MeshCollider mesh references in {SCENE_PATH}.")


if __name__ == "__main__":
    main()
