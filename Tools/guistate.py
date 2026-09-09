# -*- coding: utf-8 -*-
"""GUI 전역 상태를 바꿔 놓고 되돌리지 않는 메서드를 찾는다.

Text.Anchor / Text.Font / GUI.color 는 창 하나가 아니라 그 프레임 전체가 공유한다.
한 곳에서 바꿔 놓고 안 되돌리면 그 다음에 그려지는 남의 창 글자가 틀어진다.
눈으로는 "가끔 이상해 보인다"로만 나타나므로 여기서 잡는다.

메서드 단위로 본다. 바꾸는 대입이 하나라도 있는데 기본값으로 되돌리는 대입이
하나도 없으면 알린다. 되돌리는 쪽이 있으면 통과 - 분기마다 세지는 않는다.

둘째 검사: Listing_Standard 를 만들면서 maxOneColumn 을 안 켜는 자리.

바닐라 Listing.GetRect 는 그리는 줄마다 NewColumnIfNeeded 를 부르고, 내용이
listingRect.height 를 넘으면 curY 를 0 으로 되돌리면서 curX 를 한 칸 너비만큼
오른쪽으로 밀어 나머지를 화면 밖으로 내보낸다. 예외도 로그도 없다 - 그냥 사라진다.

더 나쁜 것은 CurHeight 가 curY 라서 **마지막 칸의 높이만** 답한다는 점이다. 그 값을
스크롤 높이로 삼아 다음 프레임에 다시 쓰면 더 일찍 넘치고, 몇 프레임이면 창이
통째로 비어 버린다 - 자기강화 고장이다. 모드 설정 창이 실제로 이걸로 무너졌다
(2026-09-10). 두 칸으로 나누고 싶은 창은 여기 없으므로 전부 못 박는다.
"""

import io
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
SRC = os.path.join(ROOT, 'Source', 'PlayableRecreation')

# (무엇을, 되돌리는 값)
WATCHED = [
    ('Text.Anchor', 'TextAnchor.UpperLeft'),
    ('Text.Font', 'GameFont.Small'),
    ('GUI.color', 'Color.white'),
]

METHOD = re.compile(
    r'^[ \t]*(?:public|private|protected|internal|static|override|virtual|sealed|new|\s)+'
    r'[\w<>\[\],\.\?]+\s+(\w+)\s*\([^;{]*\)\s*$')


def methods(text):
    """{ } 를 세어 메서드 몸통을 잘라 낸다. 문자열 안의 괄호는 무시한다."""
    lines = text.split('\n')
    found = []
    i = 0

    while i < len(lines):
        match = METHOD.match(lines[i])
        if not match or lines[i].lstrip().startswith('//'):
            i += 1
            continue

        # 다음 줄이 여는 중괄호여야 메서드다.
        j = i + 1
        while j < len(lines) and not lines[j].strip():
            j += 1
        if j >= len(lines) or lines[j].strip() != '{':
            i += 1
            continue

        depth = 0
        body = []
        k = j

        while k < len(lines):
            stripped = re.sub(r'"(?:[^"\\]|\\.)*"', '""', lines[k])
            depth += stripped.count('{') - stripped.count('}')
            body.append(lines[k])
            if depth == 0:
                break
            k += 1

        found.append((match.group(1), i + 1, '\n'.join(body)))
        i = k + 1

    return found


LISTING = re.compile(r'^[ \t]*(?:var|Listing_Standard)\s+(\w+)\s*=\s*new\s+Listing_Standard\s*\(')


def listing_problems(text, shown):
    """Listing_Standard 를 만들고 maxOneColumn 을 안 켜는 자리를 찾는다."""
    found = []
    lines = text.split('\n')

    for i, line in enumerate(lines):
        match = LISTING.match(line)
        if not match:
            continue

        # 만든 직후 몇 줄 안에 켜야 한다. Begin 뒤에 켜면 이미 늦다.
        name = match.group(1)
        window = '\n'.join(lines[i:i + 12])
        if re.search(re.escape(name) + r'\.maxOneColumn\s*=\s*true', window):
            continue

        found.append('%s:%d  %s does not set maxOneColumn '
                     '(overflow walks off the screen with no error)' % (shown, i + 1, name))

    return found


def main():
    problems = []

    for base, _, names in os.walk(SRC):
        for name in names:
            if not name.endswith('.cs'):
                continue

            path = os.path.join(base, name)
            text = io.open(path, encoding='utf-8').read()
            shown = os.path.relpath(path, ROOT).replace('\\', '/')

            problems.extend(listing_problems(text, shown))

            for method, line, body in methods(text):
                for what, restore in WATCHED:
                    sets = re.findall(re.escape(what) + r'\s*=\s*([^;]+);', body)
                    if not sets:
                        continue

                    # 되돌리는 대입이 하나라도 있으면 통과.
                    if any(restore in value for value in sets):
                        continue

                    problems.append('%s:%d  %s() sets %s and never restores it'
                                    % (shown, line, method, what))

    if problems:
        print('!! %d problems' % len(problems))
        for problem in problems:
            print('  - ' + problem)
        return 1

    print('OK - GUI state restored, every Listing_Standard pinned to one column')
    return 0


if __name__ == '__main__':
    sys.exit(main())
