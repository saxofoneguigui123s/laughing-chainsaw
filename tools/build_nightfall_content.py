#!/usr/bin/env python3
"""Build the data-driven Nightfall Survival Expansion content pack.

This script deliberately keeps the user-facing balance table in one auditable
place and generates the Unturned .dat templates, localizations, GUID registry,
loot tiers, and a distributable ZIP from it.

The generated .dat files target U3-SDK v3.26.3.12. They still need matching
Unity asset bundles (Item/Equip/Vest/Vehicle prefabs) before they can be loaded
by Unturned. See docs/INSTALL.md.
"""
from __future__ import annotations

import csv
import hashlib
import json
import re
import shutil
import sys
import uuid
import zipfile
from collections import Counter
from pathlib import Path
from typing import Any, Iterable

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "NightfallSurvivalExpansion"
RELEASE = ROOT / "release"
NAMESPACE = uuid.UUID("f3c12199-ee32-50a9-8ca8-777c2b8f3cf2")
VERSION = "3.26.3.12.3"
# Stable on purpose: this content pack is reproducible rather than dependent
# on the workstation date when a maintainer regenerates it.
RELEASE_DATE = "2026-09-29"

# The entries below are the exact requested starting stats. Other gameplay
# fields (range, fire-rate, inventory size, noise, compatible magazine, etc.)
# are intentional balance defaults and can be tuned without losing the source
# stats supplied by the project owner.
MODERN_WEAPON_ROWS = """Glock 17|Pistola|32|0.9|Comum|85
Glock 19|Pistola|34|0.8|Comum|88
Glock 18|Pistola|28|0.9|Raro|80
SIG P320|Pistola|34|0.8|Incomum|87
SIG P226|Pistola|36|1.0|Raro|92
Beretta 92FS|Pistola|35|0.95|Comum|86
FN Five-seveN|Pistola|30|0.62|Épico|88
Desert Eagle|Pistola|65|2.0|Épico|75
MP5|Submetralhadora|30|2.5|Raro|88
MP7|Submetralhadora|27|2.1|Épico|90
UMP45|Submetralhadora|42|2.5|Incomum|85
P90|Submetralhadora|29|2.6|Raro|91
Vector|Submetralhadora|33|2.7|Épico|82
Uzi|Submetralhadora|31|3.0|Incomum|80
M4A1|Fuzil|42|3.4|Raro|94
AR-15|Fuzil|41|3.2|Incomum|90
HK416|Fuzil|45|3.5|Épico|96
SCAR-L|Fuzil|47|3.6|Épico|94
SCAR-H|Fuzil pesado|65|4.0|Lendário|95
AK-47|Fuzil|48|4.3|Comum|93
AKM|Fuzil|50|3.8|Comum|95
AK-74|Fuzil|44|3.3|Incomum|92
AK-12|Fuzil|47|3.5|Raro|96
AUG|Fuzil|43|3.6|Raro|91
FAMAS|Fuzil|44|3.8|Raro|86
G36|Fuzil|42|3.6|Raro|93
Tavor X95|Fuzil|43|3.3|Épico|95
M16A4|Fuzil|44|3.8|Incomum|94
AS VAL|Fuzil especial|39|3.5|Épico|86
M24|Precisão|95|5.4|Raro|89
Remington 700|Precisão|92|4.2|Incomum|91
L96A1|Precisão|110|6.5|Épico|95
Dragunov SVD|DMR|72|4.3|Raro|90
Barrett M82|Antimaterial|180|12.5|Lendário|97
CheyTac M200|Precisão|150|12.0|Lendário|93
SR-25|DMR|70|4.8|Épico|92
Remington 870|Escopeta|90|3.5|Comum|88
Mossberg 500|Escopeta|88|3.4|Comum|90
Mossberg 590|Escopeta|92|3.6|Incomum|93
Benelli M4|Escopeta|89|3.8|Raro|95
SPAS-12|Escopeta|96|4.2|Épico|88
Saiga-12|Escopeta|91|4.0|Raro|90
KSG|Escopeta|94|3.9|Épico|92
M249|Metralhadora|52|7.5|Épico|93
PKM|Metralhadora|58|7.5|Raro|96
RPK|Metralhadora|51|5.0|Incomum|95
MG3|Metralhadora|55|11.5|Lendário|90
M134 Minigun|Metralhadora pesada|45|39.0|Lendário|98"""

ANCIENT_WEAPON_ROWS = """Espada medieval|Espada|55|1.5|Incomum|80
Espada longa|Espada|68|1.7|Raro|85
Gládio|Espada|50|1.0|Raro|82
Katana|Espada|72|1.2|Épico|78
Wakizashi|Espada|58|0.9|Raro|80
Sabre|Espada|61|1.3|Incomum|84
Espada viking|Espada|67|1.6|Raro|81
Claymore|Espada pesada|90|2.5|Épico|86
Scimitarra|Espada|62|1.4|Raro|82
Rapieira|Espada|48|1.1|Incomum|72
Machado viking|Machado|70|1.4|Raro|85
Machado de batalha|Machado|82|2.2|Épico|84
Machado de duas mãos|Machado pesado|105|4.0|Raro|80
Tomahawk|Machado|58|1.0|Incomum|82
Lança|Haste|52|2.0|Comum|75
Alabarda|Haste pesada|92|3.5|Épico|85
Tridente|Haste|60|2.5|Raro|79
Azagaia|Arremesso|55|1.5|Incomum|70
Clava|Impacto|45|1.5|Comum|90
Maça medieval|Impacto|75|2.0|Raro|92
Mangual|Impacto|82|2.5|Épico|72
Martelo de guerra|Impacto|86|2.3|Raro|88
Porrete|Impacto|38|1.0|Comum|95
Bastão|Impacto|30|1.1|Comum|90
Arco longo|Arco|65|2.0|Raro|78
Arco recurvo|Arco|60|1.5|Incomum|82
Besta|Arma de projétil|80|3.5|Raro|80
Besta pesada|Arma de projétil|110|5.0|Épico|84
Estilingue|Arma de projétil|15|0.3|Comum|70
Adaga|Lâmina|35|0.5|Comum|75
Punhal|Lâmina|42|0.6|Incomum|80
Kukri|Lâmina|65|1.0|Raro|87
Machete|Lâmina|58|1.2|Comum|90
Foice|Ferramenta/arma|54|1.6|Comum|68
Foice de guerra|Haste|78|2.2|Raro|76
Kama|Lâmina|45|0.8|Raro|72
Kusarigama|Corrente/lâmina|70|1.8|Épico|68
Chakram|Arremesso|55|0.8|Épico|75
Kunai|Arremesso|32|0.2|Incomum|70"""

ARMOR_ROWS = """Colete Kevlar básico|25|3.0|Comum|75
Colete Kevlar reforçado|35|4.0|Incomum|82
Colete IIIA|45|5.0|Raro|86
Plate Carrier leve|50|6.0|Raro|88
Plate Carrier militar|60|8.0|Épico|92
Plate Carrier pesado|70|11.0|Épico|94
Colete NIJ III|65|9.0|Épico|92
Colete NIJ IV|80|13.0|Lendário|97
Colete policial|35|4.5|Comum|80
Colete SWAT|55|8.0|Raro|90
Colete de forças especiais|68|9.5|Épico|94
Colete modular MOLLE|50|7.0|Raro|89
Colete balístico oculto|25|2.5|Incomum|72
Colete com proteção lateral|65|10.0|Épico|91
Armadura tática completa|75|14.0|Lendário|96"""

VEHICLE_ROWS = """Toyota Hilux|1000|145|80|5|Raro
Land Cruiser|1200|175|90|7|Épico
Ford F-150|1100|160|95|5|Raro
Ford Ranger|1000|150|80|5|Incomum
Chevrolet Silverado|1150|155|100|6|Raro
Jeep Wrangler|950|150|70|5|Raro
Jeep Gladiator|1050|145|80|5|Raro
RAM 1500|1150|160|95|5|Raro
Dodge Charger|850|250|75|5|Épico
Ford Mustang|800|250|60|4|Épico
Subaru Forester|900|180|65|5|Incomum
Toyota Corolla|650|180|50|5|Comum
BMW X5|900|220|80|5|Épico
Mercedes G-Class|1200|210|100|5|Lendário
Suzuki Jimny|700|145|40|4|Incomum
Mitsubishi Pajero|950|170|90|7|Raro
Nissan Patrol|1150|185|100|7|Épico
Unimog|1800|110|150|3|Lendário
Hummer H2|1400|160|120|6|Lendário
Lada Niva|750|140|50|5|Comum
Ambulância|1200|130|90|8|Raro
Viatura policial|900|200|70|5|Raro
Viatura SWAT|1300|150|100|8|Épico
Caminhão de bombeiros|2000|100|180|6|Épico
Caminhão-tanque|1800|105|250|3|Raro
Caminhão de combustível|1700|100|300|3|Épico
Van de manutenção|950|135|80|6|Incomum
Motorhome|1600|100|150|8|Épico
Ônibus escolar|1800|110|180|30|Raro
Ônibus urbano|2000|100|200|50|Raro
Caminhão blindado|2200|100|160|6|Lendário
Jipe militar|1500|140|100|5|Épico
Humvee|1800|125|95|6|Lendário
MRAP|3000|100|200|10|Lendário
M113|3500|65|300|11|Lendário
Caminhão militar|2300|100|220|20|Épico
Honda Africa Twin|400|210|24|2|Raro
Yamaha Tenere|380|190|23|2|Raro
Kawasaki KLR650|350|170|23|2|Incomum
Suzuki DR-Z400|300|145|10|2|Comum
Quadriciclo|300|90|20|2|Incomum
UTV|500|100|40|4|Raro
Buggy|350|130|35|2|Incomum
Lancha|1000|70|150|8|Raro
Barco de pesca|850|45|100|6|Incomum
Bote inflável|250|15|-|6|Comum
Jet ski|400|100|70|2|Raro"""

RARITY_TO_GAME = {
    "Comum": "Common",
    "Incomum": "Uncommon",
    "Raro": "Rare",
    "Épico": "Epic",
    "Lendário": "Legendary",
}

WEAPON_PROFILES = {
    "Pistola": dict(kind="gun", magazine="Pistol Magazine", range=100, firerate=6, action="Semi", slots=(2, 1), noise="Médio", mods=["mira", "lanterna", "supressor"]),
    "Submetralhadora": dict(kind="gun", magazine="SMG Magazine", range=150, firerate=10, action="Automatic", slots=(3, 2), noise="Alto", mods=["mira", "lanterna", "empunhadura", "supressor"]),
    "Fuzil": dict(kind="gun", magazine="Rifle Magazine", range=250, firerate=8, action="Automatic", slots=(4, 2), noise="Alto", mods=["mira", "lanterna", "empunhadura", "supressor"]),
    "Fuzil pesado": dict(kind="gun", magazine="Heavy Rifle Magazine", range=320, firerate=7, action="Automatic", slots=(5, 2), noise="Alto", mods=["mira", "lanterna", "empunhadura", "supressor"]),
    "Fuzil especial": dict(kind="gun", magazine="Rifle Magazine", range=180, firerate=9, action="Automatic", slots=(4, 2), noise="Baixo", mods=["mira", "lanterna", "empunhadura", "supressor"]),
    "Precisão": dict(kind="gun", magazine="Precision Magazine", range=500, firerate=2, action="Bolt", slots=(5, 2), noise="Alto", mods=["mira", "bipé", "supressor"]),
    "DMR": dict(kind="gun", magazine="DMR Magazine", range=400, firerate=4, action="Semi", slots=(5, 2), noise="Alto", mods=["mira", "lanterna", "empunhadura", "supressor"]),
    "Antimaterial": dict(kind="gun", magazine="Anti-Materiel Magazine", range=650, firerate=1, action="Bolt", slots=(6, 2), noise="Extremo", mods=["mira", "bipé"]),
    "Escopeta": dict(kind="gun", magazine="Shotgun Shells", range=60, firerate=4, action="Pump", slots=(4, 2), noise="Alto", mods=["mira", "lanterna", "empunhadura"]),
    "Metralhadora": dict(kind="gun", magazine="LMG Belt", range=300, firerate=9, action="Automatic", slots=(5, 3), noise="Alto", mods=["mira", "bipé", "supressor"]),
    "Metralhadora pesada": dict(kind="gun", magazine="Minigun Belt", range=350, firerate=16, action="Minigun", slots=(6, 4), noise="Extremo", mods=["mira", "bipé"]),
    "Espada": dict(kind="melee", range=2.2, slots=(3, 1), noise="Baixo"),
    "Espada pesada": dict(kind="melee", range=2.7, slots=(4, 2), noise="Médio"),
    "Machado": dict(kind="melee", range=2.0, slots=(3, 2), noise="Médio"),
    "Machado pesado": dict(kind="melee", range=2.4, slots=(4, 2), noise="Alto"),
    "Haste": dict(kind="melee", range=3.0, slots=(1, 4), noise="Médio"),
    "Haste pesada": dict(kind="melee", range=3.3, slots=(2, 4), noise="Alto"),
    "Arremesso": dict(kind="melee", range=1.7, slots=(1, 2), noise="Baixo"),
    "Impacto": dict(kind="melee", range=1.9, slots=(2, 2), noise="Médio"),
    "Arco": dict(kind="gun", magazine="Arrow Bundle", range=130, firerate=2, action="String", slots=(1, 4), noise="Baixo", mods=["mira"]),
    "Arma de projétil": dict(kind="gun", magazine="Bolt Bundle", range=120, firerate=2, action="String", slots=(3, 2), noise="Baixo", mods=["mira"]),
    "Lâmina": dict(kind="melee", range=1.5, slots=(1, 2), noise="Baixo"),
    "Ferramenta/arma": dict(kind="melee", range=2.1, slots=(3, 2), noise="Médio"),
    "Corrente/lâmina": dict(kind="melee", range=2.2, slots=(3, 2), noise="Médio"),
}

MAGAZINE_PROFILES = {
    "Pistol Magazine": (17, "Common"),
    "SMG Magazine": (30, "Uncommon"),
    "Rifle Magazine": (30, "Uncommon"),
    "Heavy Rifle Magazine": (20, "Rare"),
    "Precision Magazine": (5, "Rare"),
    "DMR Magazine": (20, "Rare"),
    "Anti-Materiel Magazine": (5, "Epic"),
    "Shotgun Shells": (8, "Common"),
    "LMG Belt": (100, "Epic"),
    "Minigun Belt": (200, "Legendary"),
    "Arrow Bundle": (12, "Uncommon"),
    "Bolt Bundle": (8, "Rare"),
}


def parse_rows(rows: str) -> Iterable[list[str]]:
    return csv.reader(rows.strip().splitlines(), delimiter="|")


def slugify(value: str) -> str:
    normalized = value.lower()
    normalized = normalized.translate(str.maketrans("áàãâäéèêëíìîïóòõôöúùûüçñ", "aaaaaeeeeiiiiooooouuuucn"))
    return re.sub(r"[^a-z0-9]+", "_", normalized).strip("_")


def stable_guid(kind: str, name: str) -> str:
    return str(uuid.uuid5(NAMESPACE, f"nightfall/{kind}/{slugify(name)}"))


def add_checksum(entry: dict[str, Any]) -> None:
    payload = json.dumps(entry, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()
    entry["content_hash"] = hashlib.sha256(payload).hexdigest()[:16]


def make_weapon(row: list[str], era: str, legacy_id: int) -> dict[str, Any]:
    name, weapon_type, damage, weight, rarity, durability = row
    if weapon_type not in WEAPON_PROFILES:
        raise ValueError(f"No profile for weapon type: {weapon_type}")
    profile = WEAPON_PROFILES[weapon_type]
    entry: dict[str, Any] = {
        "kind": "weapon",
        "era": era,
        "name": name,
        "slug": slugify(name),
        "guid": stable_guid("weapon", name),
        "legacy_id": legacy_id,
        "weapon_type": weapon_type,
        "game_item_type": "Gun" if profile["kind"] == "gun" else "Melee",
        "damage": int(damage),
        "weight_kg": float(weight),
        "rarity_pt": rarity,
        "rarity": RARITY_TO_GAME[rarity],
        "durability": int(durability),
        "range_m": profile["range"],
        "inventory_size": {"x": profile["slots"][0], "y": profile["slots"][1]},
        "noise": profile["noise"],
        "conditions": ["Novo", "Usado", "Danificado", "Quebrado"],
    }
    if profile["kind"] == "gun":
        entry.update({
            "magazine": profile["magazine"],
            "firerate": profile["firerate"],
            "action": profile["action"],
            "modifiers": profile["mods"],
        })
    else:
        entry.update({"stamina": min(100, max(8, round(float(weight) * 8))), "strength": round(1 + int(damage) / 200, 2)})
    add_checksum(entry)
    return entry


def make_armor(row: list[str], legacy_id: int) -> dict[str, Any]:
    name, protection, weight, rarity, durability = row
    protection_int = int(protection)
    entry: dict[str, Any] = {
        "kind": "armor",
        "name": name,
        "slug": slugify(name),
        "guid": stable_guid("armor", name),
        "legacy_id": legacy_id,
        "game_item_type": "Vest",
        "protection_percent": protection_int,
        # U3 represents armour as incoming-damage multiplier, rather than
        # absorption percent. 25% absorption is therefore Armor 0.75.
        "armor_multiplier": round(1 - protection_int / 100, 2),
        "weight_kg": float(weight),
        "rarity_pt": rarity,
        "rarity": RARITY_TO_GAME[rarity],
        "durability": int(durability),
        "inventory_size": {"x": 4, "y": max(3, min(6, round(float(weight) / 2)))},
        "movement_speed_multiplier": round(max(0.72, 1 - float(weight) * 0.012), 3),
        "conditions": ["Novo", "Usado", "Danificado", "Quebrado"],
    }
    add_checksum(entry)
    return entry


def vehicle_kind(name: str) -> str:
    lower = slugify(name)
    if any(token in lower for token in ("lancha", "barco", "bote", "jet_ski")):
        return "Boat"
    if any(token in lower for token in ("africa", "tenere", "klr", "dr_z", "quadriciclo", "buggy")):
        return "Car"  # Vehicle model determines motorcycle/quad handling.
    if lower == "utv":
        return "Car"
    return "Car"


def vehicle_trunk_slots(name: str, passengers: int) -> int:
    lower = slugify(name)
    if "moto" in lower or any(token in lower for token in ("africa", "tenere", "klr", "dr_z", "jet_ski")):
        return 2
    if any(token in lower for token in ("onibus", "caminhao", "motorhome")):
        return min(36, max(16, passengers + 4))
    if "bote" in lower:
        return 4
    return 14 if passengers <= 5 else min(24, passengers + 6)


def make_vehicle(row: list[str], legacy_id: int) -> dict[str, Any]:
    name, health, speed, fuel, passengers, rarity = row
    fuel_liters = None if fuel == "-" else int(fuel)
    entry: dict[str, Any] = {
        "kind": "vehicle",
        "name": name,
        "slug": slugify(name),
        "guid": stable_guid("vehicle", name),
        "legacy_id": legacy_id,
        "engine": vehicle_kind(name),
        "health": int(health),
        "speed_kmh": int(speed),
        "fuel_liters": fuel_liters,
        "passengers": int(passengers),
        "rarity_pt": rarity,
        "rarity": RARITY_TO_GAME[rarity],
        "trunk_slots": vehicle_trunk_slots(name, int(passengers)),
        "state_fields": ["Estado", "Pneus", "Motor", "Ruído"],
        "upgrades": ["blindagem", "bagageiro", "guincho", "proteção frontal"],
    }
    if fuel_liters is None:
        entry["fuel_liters"] = 0
        entry["fuel_note"] = "Não utiliza combustível"
    add_checksum(entry)
    return entry


def build_catalog() -> dict[str, Any]:
    entries: list[dict[str, Any]] = []
    legacy_id = 62000
    for row in parse_rows(MODERN_WEAPON_ROWS):
        entries.append(make_weapon(row, "moderna", legacy_id))
        legacy_id += 1
    for row in parse_rows(ANCIENT_WEAPON_ROWS):
        entries.append(make_weapon(row, "antiga", legacy_id))
        legacy_id += 1
    legacy_id = 62100
    for row in parse_rows(ARMOR_ROWS):
        entries.append(make_armor(row, legacy_id))
        legacy_id += 1
    legacy_id = 62200
    for row in parse_rows(VEHICLE_ROWS):
        entries.append(make_vehicle(row, legacy_id))
        legacy_id += 1

    magazines: list[dict[str, Any]] = []
    for index, (name, (amount, rarity)) in enumerate(MAGAZINE_PROFILES.items(), start=62500):
        entry = {
            "kind": "magazine",
            "name": name,
            "slug": slugify(name),
            "guid": stable_guid("magazine", name),
            "legacy_id": index,
            "game_item_type": "Magazine",
            "amount": amount,
            "rarity": rarity,
        }
        add_checksum(entry)
        magazines.append(entry)

    stats = {
        "weapons": sum(entry["kind"] == "weapon" for entry in entries),
        "modern_weapons": sum(entry.get("era") == "moderna" for entry in entries),
        "ancient_weapons": sum(entry.get("era") == "antiga" for entry in entries),
        "armor": sum(entry["kind"] == "armor" for entry in entries),
        "vehicles": sum(entry["kind"] == "vehicle" for entry in entries),
        "magazines": len(magazines),
        "total_runtime_assets": len(entries) + len(magazines),
    }
    return {
        "schema": "nightfall-survival-content/v1",
        "version": VERSION,
        "sdk_target": "U3-SDK v3.26.3.12",
        "generated_on": RELEASE_DATE,
        "rarity_system": {
            "Comum": "Encontrado facilmente em casas, lojas e veículos abandonados.",
            "Incomum": "Equipamento policial/militar ou versões melhores.",
            "Raro": "Difícil de encontrar, normalmente em áreas especiais.",
            "Épico": "Extremamente escasso, encontrado em bases, depósitos ou eventos.",
            "Lendário": "Equipamento de alto nível, extremamente raro e destinado ao endgame.",
        },
        "entries": entries,
        "magazines": magazines,
        "statistics": stats,
    }


def dat_lines(values: Iterable[tuple[str, Any]]) -> str:
    output = ["// Generated by tools/build_nightfall_content.py - do not hand edit."]
    for key, value in values:
        if value is None:
            continue
        if isinstance(value, float):
            value = f"{value:.3f}".rstrip("0").rstrip(".")
        output.append(f"{key} {value}")
    return "\n".join(output) + "\n"


def weapon_dat(entry: dict[str, Any], magazine_by_name: dict[str, dict[str, Any]]) -> str:
    common = [
        ("GUID", entry["guid"]), ("ID", entry["legacy_id"]), ("Type", entry["game_item_type"]),
        ("Rarity", entry["rarity"]), ("Slot", "Primary"),
        ("Size_X", entry["inventory_size"]["x"]), ("Size_Y", entry["inventory_size"]["y"]),
        ("Amount", 1), ("Quality_Min", max(10, entry["durability"] - 45)),
        ("Quality_Max", entry["durability"]), ("Use_Auto_Stat_Descriptions", True),
        ("Bypass_Hash_Verification", True),
    ]
    if entry["game_item_type"] == "Gun":
        magazine = magazine_by_name[entry["magazine"]]
        values = common + [
            ("Useable", "Gun"), ("Magazine", magazine["guid"]), ("Caliber", 0),
            ("Range", entry["range_m"]), ("Firerate", entry["firerate"]), ("Action", entry["action"]),
            ("Player_Damage", entry["damage"]), ("Player_Leg_Multiplier", 0.75),
            ("Player_Arm_Multiplier", 0.7), ("Player_Spine_Multiplier", 1),
            ("Player_Skull_Multiplier", 1.5), ("Zombie_Damage", entry["damage"]),
            ("Zombie_Leg_Multiplier", 0.75), ("Zombie_Arm_Multiplier", 0.7),
            ("Zombie_Spine_Multiplier", 1), ("Zombie_Skull_Multiplier", 1.5),
            ("Animal_Damage", entry["damage"]), ("Animal_Leg_Multiplier", 0.75),
            ("Animal_Spine_Multiplier", 1), ("Animal_Skull_Multiplier", 1.4),
            ("Durability", entry["durability"]), ("Wear", 1),
            ("Alert_Radius", {"Baixo": 10, "Médio": 20, "Alto": 38, "Extremo": 55}[entry["noise"]]),
            ("Hook_Sight", None if "mira" not in entry["modifiers"] else True),
            ("Hook_Tactical", None if "lanterna" not in entry["modifiers"] else True),
            ("Hook_Grip", None if "empunhadura" not in entry["modifiers"] else True),
            ("Hook_Barrel", None if "supressor" not in entry["modifiers"] else True),
        ]
    else:
        values = common + [
            ("Useable", "Melee"), ("Range", entry["range_m"]),
            ("Player_Damage", entry["damage"]), ("Player_Leg_Multiplier", 0.8),
            ("Player_Arm_Multiplier", 0.75), ("Player_Spine_Multiplier", 1),
            ("Player_Skull_Multiplier", 1.35), ("Zombie_Damage", entry["damage"]),
            ("Zombie_Leg_Multiplier", 0.8), ("Zombie_Arm_Multiplier", 0.75),
            ("Zombie_Spine_Multiplier", 1), ("Zombie_Skull_Multiplier", 1.35),
            ("Animal_Damage", entry["damage"]), ("Animal_Leg_Multiplier", 0.8),
            ("Animal_Spine_Multiplier", 1), ("Animal_Skull_Multiplier", 1.25),
            ("Durability", entry["durability"]), ("Wear", 1), ("Strength", entry["strength"]),
            ("Stamina", entry["stamina"]),
            ("Alert_Radius", {"Baixo": 6, "Médio": 10, "Alto": 16}[entry["noise"]]),
        ]
    return dat_lines(values)


def armor_dat(entry: dict[str, Any]) -> str:
    return dat_lines([
        ("GUID", entry["guid"]), ("ID", entry["legacy_id"]), ("Type", "Vest"),
        ("Rarity", entry["rarity"]), ("Size_X", 3), ("Size_Y", 3), ("Amount", 1),
        ("Quality_Min", max(10, entry["durability"] - 45)), ("Quality_Max", entry["durability"]),
        ("Armor", entry["armor_multiplier"]), ("Armor_Explosion", entry["armor_multiplier"]),
        ("Movement_Speed_Multiplier", entry["movement_speed_multiplier"]),
        ("Width", entry["inventory_size"]["x"]), ("Height", entry["inventory_size"]["y"]),
        ("Bypass_Hash_Verification", True),
    ])


def vehicle_dat(entry: dict[str, Any]) -> str:
    # SDK Speed_Max uses a game velocity unit. Keeping the raw requested km/h
    # alongside it in the catalog prevents loss of the design stat. This
    # conversion (km/h / 3.6) gives a physically meaningful initial target.
    speed_mps = round(entry["speed_kmh"] / 3.6, 2)
    return dat_lines([
        ("GUID", entry["guid"]), ("ID", entry["legacy_id"]), ("Type", "Vehicle"),
        ("Rarity", entry["rarity"]), ("Engine", entry["engine"]),
        ("Health_Min", max(1, int(entry["health"] * 0.45))), ("Health_Max", entry["health"]),
        ("Health", entry["health"]), ("Fuel_Min", 0), ("Fuel_Max", entry["fuel_liters"]),
        ("Fuel", entry["fuel_liters"]), ("Speed_Min", -8), ("Speed_Max", speed_mps),
        ("Brake", 16), ("Steering_Angle_Max", 32), ("Cam_Follow_Distance", 8),
        ("Bypass_Hash_Verification", True),
    ])


def magazine_dat(entry: dict[str, Any]) -> str:
    return dat_lines([
        ("GUID", entry["guid"]), ("ID", entry["legacy_id"]), ("Type", "Magazine"),
        ("Rarity", entry["rarity"]), ("Size_X", 1), ("Size_Y", 2),
        ("Amount", entry["amount"]), ("Caliber", 0), ("Pellets", 1), ("Range", 300),
        ("Player_Damage", 1), ("Zombie_Damage", 1), ("Animal_Damage", 1),
        ("Speed", 1), ("Bypass_Hash_Verification", True),
    ])


def localization(entry: dict[str, Any]) -> str:
    description: list[str]
    if entry["kind"] == "weapon":
        description = [
            f"{entry['weapon_type']} — {entry['era']}",
            f"Dano: {entry['damage']} | Peso: {entry['weight_kg']:g} kg | Durabilidade: {entry['durability']}/100",
            f"Ruído: {entry['noise']} | Estado: Novo / Usado / Danificado / Quebrado",
        ]
        if entry["game_item_type"] == "Gun":
            description.append("Modificadores: " + ", ".join(entry["modifiers"]))
    elif entry["kind"] == "armor":
        description = [
            f"Proteção: {entry['protection_percent']}% | Peso: {entry['weight_kg']:g} kg",
            f"Durabilidade: {entry['durability']}/100 | Estado: Novo / Usado / Danificado / Quebrado",
        ]
    elif entry["kind"] == "vehicle":
        fuel = entry.get("fuel_note") or f"{entry['fuel_liters']} L"
        description = [
            f"Vida: {entry['health']}/{entry['health']} | Combustível: {fuel}",
            f"Velocidade máxima: {entry['speed_kmh']} km/h | Passageiros: {entry['passengers']}",
            f"Porta-malas: {entry['trunk_slots']} slots | Melhorias: " + ", ".join(entry["upgrades"]),
        ]
    else:
        description = [f"Capacidade: {entry['amount']} | Munição Nightfall compatível"]
    return "\n".join([f"Name {entry['name']}", "Description " + " ".join(description)]) + "\n"


def write_entry_files(catalog: dict[str, Any]) -> None:
    entries = catalog["entries"]
    magazines_by_name = {entry["name"]: entry for entry in catalog["magazines"]}
    for entry in entries + catalog["magazines"]:
        category = {"weapon": "Items/Weapons", "armor": "Items/Armor", "vehicle": "Vehicles", "magazine": "Items/Magazines"}[entry["kind"]]
        asset_dir = OUTPUT / category / f"{entry['legacy_id']}_{entry['slug']}"
        asset_dir.mkdir(parents=True, exist_ok=True)
        base = asset_dir / f"{entry['slug']}.dat"
        if entry["kind"] == "weapon":
            base.write_text(weapon_dat(entry, magazines_by_name), encoding="utf-8")
        elif entry["kind"] == "armor":
            base.write_text(armor_dat(entry), encoding="utf-8")
        elif entry["kind"] == "vehicle":
            base.write_text(vehicle_dat(entry), encoding="utf-8")
        else:
            base.write_text(magazine_dat(entry), encoding="utf-8")
        (asset_dir / "English.dat").write_text(localization(entry), encoding="utf-8")
        (asset_dir / "AssetRequirements.md").write_text(asset_requirements(entry), encoding="utf-8")


def asset_requirements(entry: dict[str, Any]) -> str:
    if entry["kind"] == "weapon":
        if entry["game_item_type"] == "Gun":
            required = "Item, Equip, Projectile; add hook transforms for each listed modifier."
        else:
            required = "Item and Equip GameObjects; add appropriate attack animations/audio."
    elif entry["kind"] == "armor":
        required = "Item and Vest GameObjects. The Vest mesh must be rigged for Unturned clothing."
    elif entry["kind"] == "vehicle":
        required = f"Vehicle GameObject with Rigidbody, Seats/Seat_0 through Seat_{entry['passengers'] - 1}, Tires/ and Clip."
    else:
        required = "Item and Magazine GameObjects."
    return (
        "# Asset bundle requirements\n\n"
        f"**{entry['name']}** ({entry['guid']})\n\n"
        f"Required Unity assets: {required}\n\n"
        "The `.dat` / `English.dat` definition is complete; build a matching Unity asset bundle in U3-SDK before deploying it.\n"
    )


def build_loot_tables(catalog: dict[str, Any]) -> dict[str, Any]:
    # Spawn weights follow the requested rarity system. Tables deliberately
    # refer to GUIDs, avoiding cross-mod ID collisions.
    weights = {"Common": 60, "Uncommon": 25, "Rare": 10, "Epic": 4, "Legendary": 1}
    tables: dict[str, list[dict[str, Any]]] = {"civilian": [], "police_military": [], "special_area": [], "event_endgame": []}
    for entry in catalog["entries"] + catalog["magazines"]:
        rarity = entry["rarity"]
        record = {"guid": entry["guid"], "name": entry["name"], "weight": weights[rarity]}
        if rarity == "Common":
            target = "civilian"
        elif rarity == "Uncommon":
            target = "police_military"
        elif rarity == "Rare":
            target = "special_area"
        else:
            target = "event_endgame"
        tables[target].append(record)
    return {"schema": "nightfall-loot/v1", "weights_by_rarity": weights, "tables": tables}


def write_docs(catalog: dict[str, Any]) -> None:
    stat = catalog["statistics"]
    readme = f"""# Nightfall Survival Expansion — conteúdo v{VERSION}

Pacote de conteúdo orientado por dados para **U3-SDK v3.26.3.12**.

## Incluído

- {stat['modern_weapons']} armas modernas;
- {stat['ancient_weapons']} armas antigas;
- {stat['armor']} coletes modernos com absorção convertida para o multiplicador `Armor` nativo;
- {stat['vehicles']} veículos com vida, velocidade, combustível e passageiros;
- {stat['magazines']} carregadores/feixes de munição de apoio para as armas de fogo e projéteis.

Total: **{stat['total_runtime_assets']} definições de runtime**. Cada entrada tem GUID estável, fallback de ID legado, raridade, localização PT-BR e ficha de requisitos do prefab.

## Arquivos importantes

- `catalog.json`: fonte completa e auditável dos atributos do jogo.
- `Items/**/<item>.dat`: definições U3-SDK geradas.
- `Vehicles/**/<vehicle>.dat`: definições U3-SDK geradas.
- `LootTables.json`: tabelas de loot por nível de raridade, usando GUIDs.
- `GUID_REGISTRY.md`: lista de comandos/IDs para administração.
- `docs/INSTALL.md` no repositório: instruções para criar os asset bundles Unity exigidos pelo SDK.

> As definições incluem `Bypass_Hash_Verification` porque são entregues como fontes de conteúdo. Remova essa linha quando publicar bundles finais, para manter a verificação de integridade no servidor.
"""
    (OUTPUT / "README.md").write_text(readme, encoding="utf-8")

    lines = ["# Nightfall Survival Expansion — registro de GUIDs", "", "Os GUIDs são a referência recomendada para loot/spawners e não colidem com outros mods. IDs legados (62000–62511) são um fallback e devem ser verificados contra o servidor antes da publicação.", "", "| Tipo | Nome | ID legado | GUID |", "|---|---|---:|---|"]
    for entry in catalog["entries"] + catalog["magazines"]:
        lines.append(f"| {entry['kind']} | {entry['name']} | {entry['legacy_id']} | `{entry['guid']}` |")
    (OUTPUT / "GUID_REGISTRY.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def validate(catalog: dict[str, Any]) -> None:
    entries = catalog["entries"]
    all_entries = entries + catalog["magazines"]
    stats = catalog["statistics"]
    expected = {"modern_weapons": 48, "ancient_weapons": 39, "weapons": 87, "armor": 15, "vehicles": 47, "magazines": 12, "total_runtime_assets": 161}
    if stats != expected:
        raise AssertionError(f"Unexpected content count: {stats} != {expected}")
    for field in ("name", "slug", "guid", "legacy_id"):
        counts = Counter(entry[field] for entry in all_entries)
        duplicate = [value for value, count in counts.items() if count > 1]
        if duplicate:
            raise AssertionError(f"Duplicate {field}: {duplicate}")
    for entry in entries:
        if entry["rarity"] not in RARITY_TO_GAME.values():
            raise AssertionError(f"Unknown rarity: {entry['name']}")
        if entry["kind"] == "weapon" and not 0 < entry["durability"] <= 100:
            raise AssertionError(f"Invalid durability: {entry['name']}")
        if entry["kind"] == "armor" and not 0 < entry["protection_percent"] < 100:
            raise AssertionError(f"Invalid protection: {entry['name']}")
        if entry["kind"] == "vehicle" and entry["passengers"] < 1:
            raise AssertionError(f"Invalid passengers: {entry['name']}")


def clean_output() -> None:
    if OUTPUT.exists():
        shutil.rmtree(OUTPUT)
    OUTPUT.mkdir(parents=True)


def make_release_archive() -> Path:
    RELEASE.mkdir(exist_ok=True)
    archive = RELEASE / f"Nightfall-Survival-Expansion-content-v{VERSION}.zip"
    if archive.exists():
        archive.unlink()
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zip_file:
        for path in sorted(OUTPUT.rglob("*")):
            if path.is_file():
                zip_file.write(path, path.relative_to(OUTPUT.parent))
    return archive


def main() -> int:
    catalog = build_catalog()
    validate(catalog)
    clean_output()
    (OUTPUT / "catalog.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write_entry_files(catalog)
    (OUTPUT / "LootTables.json").write_text(json.dumps(build_loot_tables(catalog), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write_docs(catalog)
    archive = make_release_archive()
    print(f"Generated {catalog['statistics']['total_runtime_assets']} runtime definitions")
    print(f"Content: {OUTPUT}")
    print(f"Archive: {archive}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
