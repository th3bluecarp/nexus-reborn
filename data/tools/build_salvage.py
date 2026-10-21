"""Build data/salvage.sql: salvage loot for armor and weapons from Jabbithole's "Salvaged to" tables.

usage: build_salvage.py <pages dir> <Item2.json> <Salvage.json> <out.sql>

<pages dir> holds archived Jabbithole item pages named <item2 id>.html. Each page's Salvaged to tab lists what players got
from salvaging that item, as counts out of the salvages recorded; every salvage gave one item. The observations are pooled
per item class (light, medium, heavy armor, weapons, gadgets, decor) and level band, with neighbouring bands and other
classes weighted lower so sparse cells borrow from similar ones. Crafting materials are recorded by tier and handed out
as the material of the target class (ore for heavy armor, weapons and gadgets, leather for medium, cloth for light).

Every client Salvage entry (item type and level) gets an item_loot row (id 3000000 + entry id) pointing at the loot group
of its class and band (loot group ids from 3000000). The server's salvage action looks the loot up that way.
"""
import html
import json
import os
import re
import sys

ORE = [12542, 12937, 14188, 14667, 14668]          # Iron, Titanium, Platinum, Xenocite, Galactium
CLOTH = [14516, 14517, 14518, 14519, 14520]        # Canvas, Silk, Whimfiber, Manaweave, Starloom
LEATHER = [14235, 14236, 14237, 14238, 14239]      # Split, Thick, Reinforced, Rockhide, Augmented
MATERIAL_TIER = {i: t for tier in (ORE, CLOTH, LEATHER) for t, i in enumerate(tier)}
CLASS_MATERIAL = {"heavy": ORE, "weapon": ORE, "gadget": ORE, "medium": LEATHER, "light": CLOTH, "decor": LEATHER}

LIGHT = {1, 2, 3, 4, 5, 6}
MEDIUM = {8, 9, 10, 11, 12, 13}
HEAVY = {15, 16, 17, 18, 19, 20}
GADGET = {298, 299, 53, 301}
DECOR = {155}
CLASSES = ["light", "medium", "heavy", "weapon", "gadget", "decor"]
BANDS = [(5, 10), (11, 15), (16, 20), (21, 25), (26, 30), (31, 40), (41, 50), (51, 999)]
BAND_WEIGHT = {0: 1.0, 1: 0.3, 2: 0.05}
OTHER_CLASS_WEIGHT = 0.3
MIN_SHARE = 1.0  # percent

ROW = re.compile(r'<tr>\s*<td class="name">.*?items/[a-z0-9-]+?-(\d+)".*?<a class="q\d" [^>]*>([^<]+)</a>.*?'
                 r'<td class="center">\s*\d+\s*</td>\s*<td align="center">\s*[\d.]+%<br>\s*<span class="small">\((\d+)/(\d+)\)', re.S)


def item_class(type_id):
    if type_id in LIGHT:
        return "light"
    if type_id in MEDIUM:
        return "medium"
    if type_id in HEAVY:
        return "heavy"
    if type_id in GADGET:
        return "gadget"
    if type_id in DECOR:
        return "decor"
    return "weapon"


def band_of(level):
    for i, (lo, hi) in enumerate(BANDS):
        if lo <= level <= hi:
            return i
    return 0


def load(path):
    data = json.load(open(path))
    return data if isinstance(data, list) else list(data.values())


def main():
    pages, item2_path, salvage_path, out_path = sys.argv[1:5]
    items = {i["Id"]: i for i in load(item2_path)}
    salvage = [s for s in load(salvage_path) if s["Level"] > 0]
    salvage_types = {s["Item2TypeId"] for s in salvage}

    # observations: (class, band, salvages, {key: count}); key is an item id or ("mat", tier)
    observations = []
    names = {}
    for name in os.listdir(pages):
        item = items.get(int(name.split(".")[0]))
        if item is None or item["Item2TypeId"] not in salvage_types or item["PowerLevel"] < 5:
            continue
        text = open(os.path.join(pages, name), encoding="utf-8", errors="replace").read()
        tab = re.search(r'id="tab-sal2">(.*?)</table>', text, re.S)
        if not tab:
            continue
        rows = ROW.findall(tab.group(1))
        if not rows:
            continue
        counts = {}
        for item_id, item_name, n, of in rows:
            item_id = int(item_id)
            if item_id not in items:
                continue
            key = ("mat", MATERIAL_TIER[item_id]) if item_id in MATERIAL_TIER else item_id
            names[item_id] = html.unescape(item_name)
            counts[key] = counts.get(key, 0) + int(n)
        observations.append((item_class(item["Item2TypeId"]), band_of(item["PowerLevel"]), int(rows[0][3]), counts))

    groups = {}
    for cls in CLASSES:
        for band in range(len(BANDS)):
            total = 0.0
            weighted = {}
            for o_cls, o_band, salvages, counts in observations:
                w = BAND_WEIGHT.get(abs(o_band - band), 0.0) * (1.0 if o_cls == cls else OTHER_CLASS_WEIGHT)
                if w == 0.0:
                    continue
                total += salvages * w
                for key, count in counts.items():
                    weighted[key] = weighted.get(key, 0.0) + count * w
            if total == 0.0:
                continue
            shares = {}
            for key, value in weighted.items():
                item_id = CLASS_MATERIAL[cls][key[1]] if isinstance(key, tuple) else key
                shares[item_id] = shares.get(item_id, 0.0) + 100.0 * value / total
            shares = {k: v for k, v in shares.items() if v >= MIN_SHARE}
            scale = 100.0 / sum(shares.values())
            groups[(cls, band)] = (total, sorted(((k, v * scale) for k, v in shares.items()), key=lambda kv: -kv[1]))

    material_names = {12542: "Iron Chunk", 12937: "Titanium Chunk", 14188: "Platinum Chunk", 14667: "Xenocite Chunk", 14668: "Galactium Chunk",
                      14516: "Canvas Cloth", 14517: "Silk Cloth", 14518: "Whimfiber Cloth", 14519: "Manaweave Cloth", 14520: "Starloom Cloth",
                      14235: "Split Leather", 14236: "Thick Leather", 14237: "Reinforced Leather", 14238: "Rockhide Leather", 14239: "Augmented Leather"}
    names.update(material_names)

    def q(s):
        return "'" + s.replace("\\", "\\\\").replace("'", "''") + "'"

    out = ["-- Salvage loot for armor and weapons, from the items players recorded getting when salvaging, on Jabbithole.",
           "-- Built by tools/build_salvage.py. Loot group ids and item loot ids from 3000000 (item loot id = 3000000 + client Salvage id).",
           "DELETE FROM `item_loot` WHERE `id` >= 3000000 AND `id` < 4000000;",
           "DELETE FROM `loot_item` WHERE `id` >= 3000000 AND `id` < 4000000;",
           "DELETE FROM `loot_group` WHERE `id` >= 3000000 AND `id` < 4000000;"]
    group_ids = {}
    for n, ((cls, band), (total, shares)) in enumerate(sorted(groups.items()), start=1):
        group_id = 3000000 + n
        group_ids[(cls, band)] = group_id
        lo, hi = BANDS[band]
        label = f"salvage {cls} level {lo}-{hi if hi < 999 else '+'} ({total:.0f} weighted salvages)"
        out.append("INSERT INTO `loot_group` (`id`,`parentId`,`probability`,`minCount`,`maxCount`,`conditionType`,`condition`,`comment`) "
                   f"VALUES ({group_id},NULL,100,1,1,0,0,{q(label)});")
        values = ",".join(f"({group_id},0,{item_id},{share:.2f},1,1,{q(names.get(item_id, str(item_id)))})" for item_id, share in shares)
        out.append(f"INSERT INTO `loot_item` (`id`,`type`,`staticId`,`probability`,`minCount`,`maxCount`,`comment`) VALUES {values};")

    rows = []
    for s in salvage:
        key = (item_class(s["Item2TypeId"]), band_of(s["Level"]))
        if key in group_ids:
            comment = "salvage type %d level %d" % (s["Item2TypeId"], s["Level"])
            rows.append(f"({3000000 + s['Id']},{group_ids[key]},{q(comment)})")
    for i in range(0, len(rows), 500):
        out.append("INSERT INTO `item_loot` (`id`,`lootGroupId`,`comment`) VALUES " + ",".join(rows[i:i + 500]) + ";")

    open(out_path, "w", newline="\n").write("\n".join(out) + "\n")
    print(f"{len(observations)} items with salvage data, {len(groups)} loot groups, {len(rows)} salvage entries")


if __name__ == "__main__":
    main()
