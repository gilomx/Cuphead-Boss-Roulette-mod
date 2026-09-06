"""Verify the installed game's corn/aircraft hitbox mismatch (requires UnityPy).

Read-only companion to verify_native_baroness_miniboss_contract.ps1. This
checks source prefabs, not a simulated gameplay session. Unity 2017.4 does
not produce solid kinematic/static contacts with full contacts disabled:
https://docs.unity3d.com/2017.4/Documentation/ScriptReference/Rigidbody2D-useFullKinematicContacts.html
"""

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "obj" / (
    "interaction-pydeps-py312" if sys.version_info[:2] == (3, 12)
    else "interaction-pydeps"
)))
import UnityPy


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data", type=Path, default=Path(
        r"C:\Program Files (x86)\Steam\steamapps\common\Cuphead\Cuphead_Data"
    ))
    args = parser.parse_args()
    corn_names = {"Baroness_Large_Candy_Corn", "Baroness_Large_Mini_Candy_Corn"}
    plane_names = {
        "Plane_Weapon_Peashot_Basic", "Plane_Weapon_Chalice_3Way",
        "Plane_Weapon_Bomb_Basic", "Plane_Weapon_Chalice_Bomb_Basic",
    }
    ground_name = "Level_Weapon_Peashot_Basic"
    wanted = corn_names | plane_names | {ground_name}
    env = UnityPy.load(*(str(args.data / name) for name in (
        "sharedassets8.assets", "sharedassets9.assets", "sharedassets21.assets"
    )))
    found = set()
    for obj in env.objects:
        if obj.type.name != "GameObject":
            continue
        go = obj.read()
        if go.m_Name not in wanted:
            continue
        components = [ref.component.deref() for ref in go.m_Component]
        colliders = [component.read_typetree(check_read=False)
                     for component in components if component.type.name.endswith("Collider2D")]
        colliders = [collider for collider in colliders if collider["m_Enabled"]]
        if not colliders:
            continue  # A weapon controller may share its projectile's name.
        require(go.m_Name not in found, "Ambiguous hitbox prefab: " + go.m_Name)
        found.add(go.m_Name)
        bodies = [component.read_typetree(check_read=False)
                  for component in components if component.type.name == "Rigidbody2D"]
        if go.m_Name in corn_names:
            require(len(bodies) == 1 and bodies[0]["m_BodyType"] == 1
                    and bodies[0]["m_Simulated"]
                    and not bodies[0]["m_UseFullKinematicContacts"],
                    go.m_Name + " must retain its native kinematic body")
            require(all(not collider["m_IsTrigger"] for collider in colliders),
                    go.m_Name + " must have native solid hitboxes")
        elif go.m_Name in plane_names:
            require(not bodies and all(not collider["m_IsTrigger"] for collider in colliders),
                    go.m_Name + " must have implicit static, solid hitboxes")
        else:
            require(all(collider["m_IsTrigger"] for collider in colliders),
                    "Ground peashot must use native trigger contacts")
        print("PASS", go.m_Name)
    require(found == wanted, "Missing native prefabs: " + str(sorted(wanted - found)))
    print("Native aircraft/corn collision contract passed; gameplay still requires verification.")


if __name__ == "__main__":
    main()
