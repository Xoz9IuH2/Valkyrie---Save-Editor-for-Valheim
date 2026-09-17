"""Extract item limits from the installed Unity assets, not a stale item list."""
import json
import csv
import io
import sys
from pathlib import Path
import UnityPy

root = Path(sys.argv[1]) / 'valheim_Data' / 'StreamingAssets' / 'SoftRef'
env = UnityPy.load(str(root / 'Bundles' / 'c4210710'))
env.load_file(str(root / 'Bundles' / '6a33a62'))
translations = {}
english = {}
for asset in UnityPy.load(str(root.parent.parent / 'resources.assets')).objects:
    if asset.type.name != 'TextAsset':
        continue
    data = asset.read()
    text = data.m_Script
    if isinstance(text, bytes):
        text = text.decode('utf-8', errors='replace')
    rows = list(csv.reader(io.StringIO(text)))
    if not rows or 'Russian' not in rows[0]:
        continue
    column = rows[0].index('Russian')
    en_column = rows[0].index('English')
    for row in rows[1:]:
        if len(row) > column and row[column]:
            translations['$' + row[0]] = row[column]
        if len(row) > en_column and row[en_column]:
            english['$' + row[0]] = row[en_column]
items = []
icons = Path(sys.argv[2]).parent / 'Icons'
icons.mkdir(exist_ok=True)
icon_errors = []
allowed = set()
for obj in env.objects:
    if obj.type.name == 'TextAsset':
        data = obj.read()
        if 'local' in data.m_Name.lower() or 'language' in data.m_Name.lower():
            print('TEXT', data.m_Name, repr(data.m_Script[:150]))
    if obj.type.name != 'MonoBehaviour':
        continue
    tree = obj.read_typetree()
    if 'm_items' in tree and 'm_recipes' in tree:
        print('ObjectDB items', len(tree['m_items']))
        for ptr in obj.read().m_items:
            allowed.add(ptr.read().m_Name)
    if 'm_itemData' not in tree:
        continue
    shared = tree['m_itemData']['m_shared']
    go = obj.read().m_GameObject.read()
    if not shared['m_name'].startswith('$item_'):
        continue
    kind = shared['m_itemType']
    category = ('Food' if shared.get('m_food', 0) > 0 or shared.get('m_foodStamina', 0) > 0 else
                'Weapons' if kind in (3, 4, 9, 14, 22, 23) else
                'Armor' if kind in (5, 6, 7, 11, 12, 17, 18, 24) else
                'Resources' if kind in (1, 13, 21) else 'Other')
    icon_name = ''
    try:
        pointers = obj.read().m_itemData.m_shared.m_icons
        if pointers:
            image = pointers[0].read().image
            image.thumbnail((64, 64))
            icon_name = go.m_Name + '.png'
            image.save(icons / icon_name)
    except Exception as error:
        icon_errors.append((go.m_Name, str(error)))
    # Internal variants have no dedicated player icon, or clearly identify NPC/test attacks.
    internal = not icon_name or any(word in go.m_Name.lower() for word in
        ('_attack', '_shoot', 'test', 'debug', 'projectile')) or (
        category == 'Weapons' and go.m_Name.lower().startswith(('dverger', 'charred', 'draugr', 'skeleton', 'goblin')))
    items.append({'Prefab': go.m_Name, 'Name': translations.get(shared['m_name'], go.m_Name),
                  'Category': category, 'Internal': internal, 'Icon': icon_name,
                  'EnglishName': english.get(shared['m_name'], go.m_Name),
                  'MaxStack': shared['m_maxStackSize'], 'MaxQuality': shared['m_maxQuality'],
                  'Durability': shared['m_maxDurability'], 'DurabilityPerLevel': shared['m_durabilityPerLevel']})
if allowed:
    items = [item for item in items if item['Prefab'] in allowed]
print(json.dumps(items[:3], ensure_ascii=True))
print('Items:', len(items))
print('Translations:', len(translations))
print('Icon errors:', len(icon_errors), icon_errors[:3])
Path(sys.argv[2]).write_text(json.dumps(items, ensure_ascii=True, indent=2), encoding='utf-8')
