# -*- coding: utf-8 -*-
"""배포용 폴더를 굽는다.

림월드의 창작마당 업로더는 모드 폴더를 **통째로** 올린다. 이 저장소가 곧 모드
폴더이므로, 그대로 올리면 .git 과 Tests/ 와 Tools/ 까지 따라 올라간다. 용량도
용량이지만 .git 에는 커밋 기록이 들어 있다 - 저자 이름을 바꾼 의미가 없어진다.

그래서 올릴 것만 따로 담은 폴더를 만든다. 여기 적힌 목록이 곧 "모드란 무엇인가"다.

    python Tools/package.py             굽기만 한다
    python Tools/package.py --install   굽고 나서 Mods 폴더로 복사한다
"""

import io
import os
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST = os.path.join(ROOT, 'dist')
NAME = 'Playable Recreation'

MODS = r'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods'

# 올라가는 것. 이 목록에 없으면 올라가지 않는다.
FOLDERS = ['About', 'Defs', 'Languages', 'Patches', 'Textures']
ASSEMBLY = os.path.join('Assemblies', 'PlayableRecreation.dll')

# 폴더 안에서도 걸러 내는 것.
SKIP_NAMES = {'.git', '.vs', 'bin', 'obj', '__pycache__', 'Thumbs.db', '.DS_Store'}
SKIP_SUFFIX = ('.pdb', '.user', '.suo', '.deps.json')

PREVIEW_LIMIT = 1024 * 1024      # 스팀이 받아 주는 미리보기 그림의 한계


def fail(message):
    print('!! ' + message)
    sys.exit(1)


def newest_source():
    """가장 최근에 손댄 .cs 의 시각. DLL 이 그보다 오래되었으면 빌드를 안 한 것이다."""
    newest = 0.0
    where = None

    for base, dirs, names in os.walk(os.path.join(ROOT, 'Source')):
        dirs[:] = [d for d in dirs if d not in SKIP_NAMES]
        for name in names:
            if not name.endswith('.cs'):
                continue

            path = os.path.join(base, name)
            stamp = os.path.getmtime(path)
            if stamp > newest:
                newest, where = stamp, path

    return newest, where


def check():
    """구울 수 있는 상태인지 본다. 여기서 걸리는 것은 전부 창작마당에서 걸릴 것들이다."""
    about = os.path.join(ROOT, 'About', 'About.xml')
    if not os.path.exists(about):
        fail('About/About.xml 이 없다')

    meta = ET.parse(about).getroot()
    for field in ['name', 'author', 'packageId', 'modVersion', 'supportedVersions']:
        if meta.find(field) is None:
            fail('About.xml 에 <%s> 가 없다' % field)

    versions = [li.text for li in meta.find('supportedVersions')]
    print('  %s %s · %s · RimWorld %s'
          % (meta.find('name').text, meta.find('modVersion').text,
             meta.find('packageId').text, ', '.join(versions)))

    dll = os.path.join(ROOT, ASSEMBLY)
    if not os.path.exists(dll):
        fail('%s 가 없다. dotnet build -c Release 를 먼저 돌려라' % ASSEMBLY)

    stamp, where = newest_source()
    if stamp > os.path.getmtime(dll):
        fail('DLL 이 소스보다 오래되었다 - %s 를 고치고 빌드하지 않았다'
             % os.path.relpath(where, ROOT).replace('\\', '/'))

    preview = os.path.join(ROOT, 'About', 'Preview.png')
    if not os.path.exists(preview):
        fail('About/Preview.png 이 없다')
    if os.path.getsize(preview) > PREVIEW_LIMIT:
        fail('Preview.png 이 %.1f MB 다. 스팀은 1 MB 까지만 받는다'
             % (os.path.getsize(preview) / 1024.0 / 1024.0))

    if not os.path.exists(os.path.join(ROOT, 'About', 'ModIcon.png')):
        print('  (참고) About/ModIcon.png 이 없다. 모드 목록에 아이콘이 안 뜬다')

    published = os.path.join(ROOT, 'About', 'PublishedFileId.txt')
    if os.path.exists(published):
        print('  기존 창작마당 항목을 갱신한다 (id %s)'
              % io.open(published, encoding='utf-8').read().strip())
    else:
        print('  창작마당에 새 항목을 만든다 (PublishedFileId.txt 없음)')


def wanted(base, names):
    return [n for n in names if n not in SKIP_NAMES and not n.endswith(SKIP_SUFFIX)]


def bake():
    out = os.path.join(DIST, NAME)
    if os.path.exists(out):
        shutil.rmtree(out)
    os.makedirs(out)

    for folder in FOLDERS:
        source = os.path.join(ROOT, folder)
        if not os.path.isdir(source):
            continue

        shutil.copytree(source, os.path.join(out, folder),
                        ignore=lambda base, names: set(names) - set(wanted(base, names)))

    os.makedirs(os.path.join(out, 'Assemblies'))
    shutil.copy2(os.path.join(ROOT, ASSEMBLY), os.path.join(out, ASSEMBLY))

    for extra in ['LICENSE', 'LICENSE.txt', 'README.md']:
        path = os.path.join(ROOT, extra)
        if os.path.exists(path):
            shutil.copy2(path, os.path.join(out, extra))

    return out


def measure(folder):
    count = 0
    total = 0

    for base, dirs, names in os.walk(folder):
        for name in names:
            count += 1
            total += os.path.getsize(os.path.join(base, name))

    return count, total


def install(out):
    if not os.path.isdir(MODS):
        fail('Mods 폴더를 찾지 못했다: %s' % MODS)

    stale = [n for n in os.listdir(MODS)
             if os.path.islink(os.path.join(MODS, n))
             and os.path.realpath(os.path.join(MODS, n)) == os.path.realpath(ROOT)]

    if stale:
        print()
        print('!! Mods 안에 이 저장소를 가리키는 링크가 있다: ' + ', '.join(stale))
        print('   그대로 두면 같은 packageId 가 둘이 되어 림월드가 하나를 버린다.')
        print('   먼저 지워라:  rm "%s"' % os.path.join(MODS, stale[0]))
        sys.exit(1)

    target = os.path.join(MODS, NAME)
    if os.path.exists(target):
        # 창작마당 항목 번호는 업로드로만 생긴다. 지우기 전에 챙겨 둔다.
        keep = os.path.join(target, 'About', 'PublishedFileId.txt')
        if os.path.exists(keep) and not os.path.exists(os.path.join(out, 'About', 'PublishedFileId.txt')):
            shutil.copy2(keep, os.path.join(out, 'About', 'PublishedFileId.txt'))
            shutil.copy2(keep, os.path.join(ROOT, 'About', 'PublishedFileId.txt'))
            print('  Mods 쪽 PublishedFileId.txt 를 저장소로 가져왔다')

        shutil.rmtree(target)

    shutil.copytree(out, target)
    print('  설치: %s' % target)


def main():
    print('굽는 중...')
    check()

    out = bake()
    count, total = measure(out)

    print('  %d 개 파일 · %.0f KB → %s'
          % (count, total / 1024.0, os.path.relpath(out, ROOT).replace('\\', '/')))

    if '--install' in sys.argv:
        install(out)

    print('OK')
    return 0


if __name__ == '__main__':
    sys.exit(main())
