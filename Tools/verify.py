# -*- coding: utf-8 -*-
"""배포 전 점검: XML 유효성 + 번역 키 커버리지.

동적으로 조립되는 키는 MiniGameDef 를 직접 읽어서 만들어 낸다 -
게임을 하나 더 붙여도 이 파일은 고치지 않아도 된다.
"""
import io, os, re, sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KEYED = {
    'KO': ROOT + u'/Languages/Korean (\ud55c\uad6d\uc5b4)/Keyed',
    'EN': ROOT + u'/Languages/English/Keyed',
}
GAMES = ROOT + u'/Defs/MiniGameDefs/MiniGames_PR.xml'

TIERS = 5
REASONS = ['Combat', 'Cleaning', 'Repair', 'Damaged', 'Moved', 'Destroyed', 'Expired']

problems = []


def walk(base, suffix):
    found = []
    for path, dirs, files in os.walk(base):
        if 'obj' in path or 'bin' in path or '.git' in path:
            continue
        for f in files:
            if f.endswith(suffix):
                found.append(os.path.join(path, f))
    return found


# ---------- 1. 모든 XML 파싱 ----------
xml_files = walk(ROOT, '.xml')
for path in xml_files:
    try:
        ET.parse(path)
    except Exception as e:
        problems.append('XML parse: %s -> %s' % (path, e))

print('XML files parsed: %d' % len(xml_files))

# ---------- 2. 키 수집 ----------
def keys_of(folder):
    keys, dup = set(), set()
    for path in sorted(walk(folder, '.xml')):
        for child in ET.parse(path).getroot():
            if child.tag in keys:
                dup.add(child.tag)
            keys.add(child.tag)
    return keys, dup


sets = {}
for label, folder in KEYED.items():
    sets[label], dup = keys_of(folder)
    for t in sorted(dup):
        problems.append('%s duplicate key: %s' % (label, t))

ko_keys, en_keys = sets['KO'], sets['EN']
print('ko keys: %d, en keys: %d' % (len(ko_keys), len(en_keys)))

for k in sorted(ko_keys - en_keys):
    problems.append('missing in EN: ' + k)
for k in sorted(en_keys - ko_keys):
    problems.append('missing in KO: ' + k)

# ---------- 3. 코드가 쓰는 키 ----------
# 접두사는 Keyed 파일 이름에서 뽑는다 - 게임을 더 붙여도 이 파일은 고치지 않는다.
PREFIXES = sorted(set(os.path.splitext(os.path.basename(p))[0]
                      for p in walk(KEYED['KO'], '.xml')))
PREFIX_RE = r'"((?:%s)\.[A-Za-z0-9_.]+)"' % '|'.join(PREFIXES)
print('key prefixes: %s' % ', '.join(PREFIXES))

used = set()
for path in walk(ROOT + u'/Source', '.cs'):
    src = io.open(path, encoding='utf-8').read()
    for m in re.finditer(PREFIX_RE, src):
        used.add(m.group(1))

# 프레임워크가 조립하는 키
for i in range(TIERS):
    used.add('PR.Difficulty.T%d.Label' % i)
for r in REASONS:
    used.add('PR.Invalidation.%s.Msg' % r)
    if r != 'Destroyed':
        used.add('PR.Invalidation.%s.Label' % r)

# Def 가 지시하는 키
for game in ET.parse(GAMES).getroot():
    def text(tag, fallback=None):
        node = game.find(tag)
        return node.text.strip() if node is not None and node.text else fallback

    prefix = text('difficultyKeyPrefix')
    if prefix:
        for i in range(int(text('difficultyCount', '5'))):
            used.add('%s.T%d.Desc' % (prefix, i))

    tut = text('tutorialKeyPrefix')
    if tut:
        for i in range(1, int(text('tutorialPages', '0')) + 1):
            used.add('%s.P%d.Title' % (tut, i))
            used.add('%s.P%d.Body' % (tut, i))

# Def 안에 문자열로 박혀 있는 키(tallyKeys, modExtensions 의 ringerKey 등)
for node in ET.parse(GAMES).getroot().iter():
    if node.text and re.match(r'^(?:%s)\.[A-Za-z0-9_.]+$' % '|'.join(PREFIXES), node.text.strip()):
        used.add(node.text.strip())

# 코드가 이어 붙이는 키. "POK.Hand." + category 처럼 조각만 소스에 남으므로,
# 그 조각으로 시작하는 키는 전부 쓰인 것으로 본다.
stems = set(k for k in used if k.endswith('.') and k.count('.') >= 2)
for stem in stems:
    used |= set(k for k in ko_keys if k.startswith(stem))

# 조립용 조각은 그 자체로 키가 아니다 - 다른 키의 앞자리이기만 하면 걸러낸다.
used = set(k for k in used if not k.endswith('.'))
used = set(k for k in used
           if k in ko_keys or not any(other.startswith(k) and other != k for other in ko_keys))

for k in sorted(k for k in used if k not in ko_keys):
    problems.append('used in code but missing in KO: ' + k)

unused = sorted(k for k in ko_keys if k not in used)
print('used keys: %d, unused: %d' % (len(used), len(unused)))
for k in unused:
    print('  unused: ' + k)

# ---------- 4. 파일 구조 ----------
for rel in ['About/About.xml', 'Assemblies/PlayableRecreation.dll',
            'Patches/Patch_Recreation.xml',
            'Defs/MiniGameDefs/MiniGames_PR.xml',
            'Defs/ThoughtDefs/Thoughts_PR.xml',
            'Defs/TaleDefs/Tales_PR.xml',
            'Defs/JobDefs/Jobs_PR.xml']:
    if not os.path.exists(os.path.join(ROOT, rel)):
        problems.append('missing file: ' + rel)

# ---------- 5. Def 가 가리키는 클래스가 실제로 있는가 ----------
sources = u'\n'.join(io.open(p, encoding='utf-8').read() for p in walk(ROOT + u'/Source', '.cs'))
for game in ET.parse(GAMES).getroot():
    node = game.find('workerClass')
    if node is None or not node.text:
        continue
    name = node.text.strip().split('.')[-1]
    if ('class %s' % name) not in sources:
        problems.append('workerClass not found in source: ' + node.text.strip())

# ---------- 6. Def 가 가리키는 이름이 실제로 있는가 ----------
# 여기서 잡히는 오타는 게임을 켜야만 빨간 줄로 드러나는 종류다.
PATCH = ROOT + u'/Patches/Patch_Recreation.xml'
VANILLA = u'C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Data'

ours = set()
for path in walk(ROOT + u'/Defs', '.xml'):
    ours |= set(re.findall(r'<defName>([^<]+)</defName>', io.open(path, encoding='utf-8').read()))

vanilla = set()
if os.path.isdir(VANILLA):
    for path in walk(VANILLA, '.xml'):
        try:
            src = io.open(path, encoding='utf-8', errors='ignore').read()
        except Exception:
            continue
        vanilla |= set(re.findall(r'<defName>([^<]+)</defName>', src))

known = vanilla | ours
print('defNames known: %d (vanilla %d + ours %d)' % (len(known), len(vanilla), len(ours)))

if not vanilla:
    print('  (RimWorld 설치본을 못 찾아 바닐라 대조는 건너뜀)')

REFERENCES = ['targetThing', 'vanillaJob', 'playThought', 'wonTale', 'linkedSkill']

games = ET.parse(GAMES).getroot()
patch_src = io.open(PATCH, encoding='utf-8').read()
targets = []

for game in games:
    name = game.find('defName').text.strip()
    targets.append(game.find('targetThing').text.strip())

    if known:
        for field in REFERENCES:
            node = game.find(field)
            if node is None or not node.text:
                continue
            if node.text.strip() not in known:
                problems.append('%s: %s "%s" is not a Def' % (name, field, node.text.strip()))

    # 승부인 항목에는 집계 이름표가 있어야 기록 화면이 비지 않는다.
    flag = game.find('hasMatch')
    if flag is None or flag.text.strip().lower() != 'false':
        tally = game.find('tallyKeys')
        if tally is None or len(list(tally)) == 0:
            problems.append('%s: a match with no tallyKeys' % name)

    if '<game>%s</game>' % name not in patch_src:
        problems.append('%s: no patch entry' % name)

patched = re.findall(r'defName="([^"]+)"', patch_src)
if sorted(patched) != sorted(targets):
    problems.append('patch targets %s but Defs name %s' % (sorted(patched), sorted(targets)))

# ---------- 결과 ----------
print('')
if problems:
    print('!! %d problems' % len(problems))
    for p in problems:
        print('  - ' + p)
    sys.exit(1)

print('OK - no problems')
